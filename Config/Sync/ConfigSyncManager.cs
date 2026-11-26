// Config/Sync/ConfigSyncManager.cs
//
// PURPOSE: Multiplayer host config sync. The lobby host's Crusader DE Tweaker configs are sent to every
//          joining player and used for that lobby's matches; clients go back to their own configs when
//          they leave. Design, sources and failure modes: docs/HOST_SYNC_DESIGN.md.
//
// FLOW:
//   Register()   (Plugin.Awake)          lobby tab + ViewModel with SE (SE requires Awake).
//   Initialize() (after all config init) pack this machine's files -> LocalPackage; session hooks.
//   OnSyncEnabledSet / OnHostBlobSet     ViewModel setters; as a client these are the verified host's
//                                        values (SE checks the sender is the Steam lobby owner).
//   OnMaxCountsSet                       lobby MaxCount values (GitLab #5): applied at once through
//                                        LobbyMaxCounts on every peer, whatever the host sync switch says.
//   Reconcile()                          host sync on + valid package -> ApplyHost(); off -> Revert().
//   Tick() (FrameDriver, 1 s)            role changes; revert once no longer a multiplayer client.
//   BeforeTemplateReapply()              called by ConfigLoader.ReapplyTemplateConfigs (every SE unload
//                                        reset and session start): drop the host's files if no longer a client.
//   OnStartMap / OnLoadSave (Pre)        match-start log lines; host-side "files changed" warning.
//
// IMPORTANT FOR AI AGENTS:
// - The player's own config files are NEVER written here. Host files go to ConfigPaths.HostSyncDir and
//   the loaders are redirected with ConfigPaths.UseHostSyncFiles; multipliers are overridden in memory
//   (BepInExConfigManager.ApplyHostMultipliers, autosave off).
// - Apply and revert go through ConfigLoader.ReapplyTemplateConfigs, the same entry point that runs after
//   every SE map-unload reset (SE clears the unit/building tables on each unload, v2.6.7) and before every
//   session start. It starts with TemplateBaseline.RestoreAll() so the -1 sentinel cannot leak one side's
//   overrides into the other (design doc section 8). Never apply synced values any other way: they would
//   not survive the next unload.
// - GameplaySettings is session state: it is applied by the ConfigLoader session hooks (Post), which
//   read ConfigPaths.Globals and so pick up the host's file. Never write it from here.
// - Every log line starts with [ConfigSync]; users paste these into bug reports.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.Toml;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;

namespace CrusaderDETweaker.Config.Sync
{
    internal static class ConfigSyncManager
    {
        private const string Tag = "[ConfigSync]";
        private const string XamlPath = "ScriptExtenderUI/CDTLobbySettings.xaml";
        private const float TickSeconds = 1f;
        private const float NothingReceivedSeconds = 15f;

        private enum Role { None, Host, Client }

        internal static ConfigSyncLobbySettings Lobby { get; private set; }

        /// <summary>This machine's package, built at launch; what the host sends. Null before Initialize.</summary>
        internal static byte[] LocalPackage { get; private set; }

        private static bool _initialized;
        private static byte[] _localHash;
        private static int _localFileCount;
        private static string _localError;

        // The files packed at launch (what a host sends); the host applies exactly these while hosting with sync on.
        private static Dictionary<SyncFileId, byte[]> _localEntries;

        // This player's own SyncEnabled preference (the ViewModel field holds the host's while a client).
        private static bool _ownSyncEnabled = true;

        // This player's own lobby MaxCount values (same rule as _ownSyncEnabled).
        private static string _ownMaxCounts = string.Empty;

        // Client state for the current lobby.
        private static bool? _hostSyncEnabled;
        private static ConfigSyncPackage _hostPackage;
        private static string _hostPackageError;
        private static string _problem;
        private static bool _applied;
        private static byte[] _appliedHash;
        private static float _clientSince = -1f;
        private static bool _nothingReceivedWarned;
        private static string _clientHostId;

        private static Role _lastRole = Role.None;
        private static float _nextTick;

        // ------------------------------------------------------------------
        // Startup
        // ------------------------------------------------------------------

        /// <summary>Register the lobby tab. Called from Plugin.Awake (SE: register during Awake).</summary>
        internal static void Register(BaseUnityPlugin plugin)
        {
            try
            {
                Lobby = new ConfigSyncLobbySettings();
                // The mod name is SE's packet key: identical on every peer, never localized or versioned.
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(plugin, PluginInfo.PLUGIN_NAME, Lobby, XamlPath);

                bool registered = GameXAMLManagerAPI.Instance.RegisteredModSettings.Any(e => ReferenceEquals(e.ViewModel, Lobby));
                if (registered)
                    Plugin.Logger.LogInfo($"{Tag} Lobby tab '{PluginInfo.PLUGIN_NAME}' registered. Host config sync is {(_ownSyncEnabled ? "ON" : "OFF")} when you host.");
                else
                    Plugin.Logger.LogError($"{Tag} Lobby tab NOT registered (SE logged the reason above, e.g. '{XamlPath}' missing). Host config sync is unavailable this session; the mod otherwise works.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"{Tag} Lobby tab registration failed, host config sync unavailable (the mod otherwise works): {ex.Message}");
            }
        }

        /// <summary>
        /// Pack this machine's configs and register the session hooks. Called once from Plugin after
        /// ConfigManager.Initialize() and BepInExConfigManager.Initialize().
        /// </summary>
        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildLocalPackage();

            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => OnSessionStarting("OnStartMap"));

            MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Pre && !args.LoadingEditorMap)
                .Subscribe(_ => OnSessionStarting("OnLoadSave"));

            UpdateStatus();
        }

        private static void BuildLocalPackage()
        {
            try
            {
                var entries = ReadOwnEntries(out int rawBytes);
                LocalPackage = ConfigSyncCodec.Pack(ConfigSyncCodec.WireVersion(PluginInfo.PLUGIN_VERSION, PluginInfo.SIMULATION_VERSION),
                                                    entries, out _localHash, out int bodyBytes);
                _localEntries = entries;
                _localFileCount = entries.Count;
                _localError = null;
                Plugin.Logger.LogInfo($"{Tag} Packed your configs for hosting: {entries.Count} files, {rawBytes} bytes raw, {LocalPackage.Length} bytes on the wire, hash {ConfigSyncCodec.ShortHash(_localHash)}, v{PluginInfo.PLUGIN_VERSION}.");
            }
            catch (Exception ex)
            {
                // Clients then receive an empty package and report it; the local game is unaffected.
                LocalPackage = Array.Empty<byte>();
                _localHash = null;
                _localEntries = null;
                _localError = ex.Message;
                Plugin.Logger.LogError($"{Tag} Could not pack your configs for hosting: {ex.Message}. Players joining your lobby will keep their own configs.");
            }
        }

        /// <summary>Read this player's own files (never the HostSync folder) and multiplier values.</summary>
        private static Dictionary<SyncFileId, byte[]> ReadOwnEntries(out int rawBytes)
        {
            var entries = new Dictionary<SyncFileId, byte[]>();
            rawBytes = 0;
            foreach (var id in ConfigSyncCodec.AllIds)
            {
                byte[] data;
                if (id == SyncFileId.GlobalMultipliers)
                {
                    var values = BepInExConfigManager.GetMultiplierValues();
                    if (values.Count == 0)
                    {
                        Plugin.Logger.LogWarning($"{Tag} No multiplier values found; GlobalMultipliers is not synced.");
                        continue;
                    }
                    data = ConfigSyncCodec.FormatMultipliers(values);
                }
                else
                {
                    string path = Path.Combine(ConfigPaths.OwnConfigDir, ConfigSyncCodec.RelativePathFor(id));
                    if (!File.Exists(path))
                    {
                        Plugin.Logger.LogWarning($"{Tag} {path} not found; it is not synced (players use the game values for it, as you do).");
                        continue;
                    }
                    data = File.ReadAllBytes(path);
                    if (data.Length == 0)
                    {
                        Plugin.Logger.LogWarning($"{Tag} {path} is empty; it is not synced.");
                        continue;
                    }
                }
                entries[id] = data;
                rawBytes += data.Length;
            }
            return entries;
        }

        // ------------------------------------------------------------------
        // Values from the ViewModel
        // ------------------------------------------------------------------

        internal static void OnSyncEnabledSet(bool value)
        {
            Role role = CurrentRole();
            if (role != Role.Client)
            {
                // Our own preference (UI click as host / in the menu, or SE restoring it at startup).
                _ownSyncEnabled = value;
                if (role == Role.Host)
                    Plugin.Logger.LogInfo($"{Tag} You turned host config sync {(value ? "ON" : "OFF")}; players in your lobby are told now.");
                UpdateStatus();
                return;
            }

            if (_hostSyncEnabled == value) return;
            _hostSyncEnabled = value;
            Plugin.Logger.LogInfo($"{Tag} Host config sync is {(value ? "ON" : "OFF")} (from lobby owner {HostIdText()}).");
            Reconcile();
        }

        /// <summary>
        /// Lobby MaxCount values from the ViewModel: this player's own (UI edit, SE restoring them at startup)
        /// or, as a client, the lobby owner's. Either way they replace the lobby values in effect now.
        /// </summary>
        internal static void OnMaxCountsSet(string raw, string normalized)
        {
            Role role = CurrentRole();
            if (role != Role.Client) _ownMaxCounts = normalized;
            string source = role == Role.Client ? $"lobby owner {HostIdText()}" : role == Role.Host ? "you, as host" : "you";
            LobbyMaxCounts.SetLobbyValues(raw, source);
        }

        internal static void OnHostBlobSet(byte[] blob)
        {
            if (CurrentRole() != Role.Client)
            {
                // SE only delivers host-only values to clients; anything else is not the host's package.
                Plugin.Logger.LogDebug($"{Tag} Ignored a package write while not a multiplayer client.");
                return;
            }

            int size = blob?.Length ?? 0;
            if (ConfigSyncCodec.TryUnpack(blob, out var package, out string error))
            {
                _hostPackage = package;
                _hostPackageError = null;
                Plugin.Logger.LogInfo($"{Tag} Received the host's configs from lobby owner {HostIdText()}: {size} bytes, {package.Entries.Count} files, hash {ConfigSyncCodec.ShortHash(package.BodyHash)}, host v{package.HostModVersion}. Verified OK.");
            }
            else
            {
                _hostPackage = null;
                _hostPackageError = error;
                Plugin.Logger.LogError($"{Tag} REJECTED the host's configs from lobby owner {HostIdText()} ({size} bytes): {error}. Your own configs apply.");
            }
            Reconcile();
        }

        // ------------------------------------------------------------------
        // Client: decide, apply, revert
        // ------------------------------------------------------------------

        private static void Reconcile()
        {
            if (_hostSyncEnabled == false)
            {
                _problem = null;
                Revert("the host turned config sync off");
                UpdateStatus();
                return;
            }

            if (_hostPackageError != null)
            {
                _problem = $"The host's configs were rejected ({_hostPackageError}). Your own configs apply. See LogOutput.log.";
                Revert("the host's package was rejected");
                UpdateStatus();
                return;
            }

            if (_hostSyncEnabled == null || _hostPackage == null)
            {
                UpdateStatus();
                return;
            }

            if (_applied && ConfigSyncCodec.BytesEqual(_appliedHash, _hostPackage.BodyHash))
            {
                UpdateStatus();
                return;
            }

            ApplyHost(_hostPackage);
            UpdateStatus();
        }

        private static void ApplyHost(ConfigSyncPackage package)
        {
            string hash = ConfigSyncCodec.ShortHash(package.BodyHash);

            var verdict = ConfigSyncCodec.CompareVersions(package.HostModVersion, PluginInfo.PLUGIN_VERSION, PluginInfo.SIMULATION_VERSION);
            if (verdict == ConfigSyncCodec.VersionVerdict.SimulationDiffers)
            {
                ConfigSyncCodec.SplitWireVersion(package.HostModVersion, out string hostVersion, out int? hostSimulation);
                Plugin.Logger.LogError($"{Tag} REJECTED the host's configs: host runs v{hostVersion}, you run v{PluginInfo.PLUGIN_VERSION}, and these versions do not play the same files the same way " +
                                       $"(simulation version host {(hostSimulation.HasValue ? hostSimulation.Value.ToString() : "unknown, before 2.11.8")}, you {PluginInfo.SIMULATION_VERSION}). Your own configs apply. Use the same Crusader DE Tweaker version.");
                _problem = $"The host runs Crusader DE Tweaker v{hostVersion}, you v{PluginInfo.PLUGIN_VERSION}: these play differently. Use the same version. Your own configs apply.";
                Revert("the host's version plays differently");
                return;
            }
            if (verdict == ConfigSyncCodec.VersionVerdict.Incompatible)
            {
                Plugin.Logger.LogError($"{Tag} REJECTED the host's configs: host runs v{package.HostModVersion}, you run v{PluginInfo.PLUGIN_VERSION} (major.minor must match). Your own configs apply.");
                _problem = $"The host runs an incompatible Crusader DE Tweaker (v{package.HostModVersion}, you v{PluginInfo.PLUGIN_VERSION}). Your own configs apply.";
                Revert("the host's version is incompatible");
                return;
            }
            if (verdict == ConfigSyncCodec.VersionVerdict.PatchDiffers)
                Plugin.Logger.LogWarning($"{Tag} Host runs v{package.HostModVersion}, you run v{PluginInfo.PLUGIN_VERSION}: same major.minor and simulation version, applying. Update to the same version to be safe.");

            // Validate the multiplier list before anything is written (all-or-nothing).
            Dictionary<string, float> hostMultipliers = null;
            if (package.Entries.TryGetValue(SyncFileId.GlobalMultipliers, out var multiplierBytes)
                && !ConfigSyncCodec.TryParseMultipliers(multiplierBytes, out hostMultipliers, out string multiplierError))
            {
                Plugin.Logger.LogError($"{Tag} REJECTED the host's configs: {multiplierError}. Your own configs apply.");
                _problem = $"The host's configs were rejected ({multiplierError}). Your own configs apply. See LogOutput.log.";
                Revert("the host's package was rejected");
                return;
            }

            try
            {
                var written = WriteHostFiles(package);

                ConfigPaths.UseHostSyncFiles = true;

                // Multiplier values first (the template multipliers read them), then the shared
                // re-apply entry point (baseline restore, Units, Structures, matrices, multiplier
                // template writes). SE resets the tables on every map unload, and ConfigLoader re-runs
                // the same entry point then and at every session start, reading the host's files for as
                // long as UseHostSyncFiles is set.
                string multiplierNote;
                if (hostMultipliers != null)
                {
                    var (applied, unknown, missing) = BepInExConfigManager.ApplyHostMultipliers(hostMultipliers);
                    multiplierNote = $"{applied} multipliers";
                    if (unknown.Count > 0) Plugin.Logger.LogWarning($"{Tag} Host multipliers this version does not have (ignored): {string.Join(", ", unknown)}");
                    if (missing.Count > 0) Plugin.Logger.LogWarning($"{Tag} Multipliers the host did not send (your own values used): {string.Join(", ", missing)}");
                }
                else
                {
                    BepInExConfigManager.RestoreOwnMultipliers();
                    multiplierNote = "no multipliers (the host sent none; your own apply)";
                }

                ConfigLoader.ReapplyTemplateConfigs("host sync applied");

                _applied = true;
                _appliedHash = package.BodyHash;
                _problem = null;
                Plugin.Logger.LogInfo($"{Tag} Applied the host's configs (hash {hash}, host v{package.HostModVersion}): {string.Join(", ", written)}; {multiplierNote}. Tables were reset to game values first. GameplaySettings from the host apply at match start. Host files: {ConfigPaths.HostSyncDir}");
                if (TemplateBaseline.UnreadableCount > 0)
                    Plugin.Logger.LogWarning($"{Tag} {TemplateBaseline.UnreadableCount} table cell(s) could not be read at launch and cannot be reset (see [TemplateBaseline] debug lines).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"{Tag} Applying the host's configs FAILED: {ex}");
                _problem = "Applying the host's configs failed. Your own configs apply. See LogOutput.log.";
                _applied = true; // force the revert below to run in full
                Revert("applying the host's configs failed");
            }
        }

        /// <summary>
        /// Write the package's files under HostSyncDir (fixed names from the whitelist, atomic). A file
        /// the package lacks is removed there so a previous host's copy is never read.
        /// </summary>
        private static List<string> WriteHostFiles(ConfigSyncPackage package)
        {
            var written = new List<string>();
            foreach (var id in ConfigSyncCodec.AllIds)
            {
                string path = Path.Combine(ConfigPaths.HostSyncDir, ConfigSyncCodec.RelativePathFor(id));
                if (package.Entries.TryGetValue(id, out var data))
                {
                    AtomicFileWriter.WriteIfChanged(path, data);
                    if (id != SyncFileId.GlobalMultipliers) written.Add(Path.GetFileName(path));
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    Plugin.Logger.LogWarning($"{Tag} The host sent no {Path.GetFileName(path)}; the game values are used for it (as on the host).");
                }
            }
            return written;
        }

        /// <summary>Back to this player's own configs. No-op when the host's are not in use.</summary>
        private static void Revert(string reason)
        {
            if (!_applied && !ConfigPaths.UseHostSyncFiles && !BepInExConfigManager.HostOverrideActive) return;

            try
            {
                SwitchToOwnConfigs();
                ConfigLoader.ReapplyTemplateConfigs("host sync ended: " + reason);
                Plugin.Logger.LogInfo($"{Tag} Reverted to your own configs ({reason}). Tables reset to game values and your files re-applied. Note: GameplaySettings keys your own file leaves at 'no override' may keep the host's value until you restart the game.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"{Tag} Reverting to your own configs FAILED ({reason}): {ex}. Restart the game to be sure your own configs apply.");
            }
            finally
            {
                _applied = false;
                _appliedHash = null;
            }
        }

        /// <summary>Stop reading the host's files and put this player's multiplier values back.</summary>
        private static void SwitchToOwnConfigs()
        {
            ConfigPaths.UseHostSyncFiles = false;
            BepInExConfigManager.RestoreOwnMultipliers();
        }

        /// <summary>
        /// Called first by ConfigLoader.ReapplyTemplateConfigs (after every SE map-unload reset and before
        /// every session start). If the host's configs are still selected but this player is no longer a
        /// multiplayer client, switch back to the player's own before the re-apply reads any file, so e.g.
        /// a single-player start right after a match can never use the host's values.
        /// </summary>
        internal static void BeforeTemplateReapply(string reason)
        {
            if ((ConfigPaths.UseHostSyncFiles || BepInExConfigManager.HostOverrideActive) && CurrentRole() != Role.Client)
            {
                SwitchToOwnConfigs();
                _applied = false;
                _appliedHash = null;
                Plugin.Logger.LogInfo($"{Tag} Reverted to your own configs ({reason}: you are no longer a multiplayer client). Your files are re-applied now. Note: GameplaySettings keys your own file leaves at 'no override' may keep the host's value until you restart the game.");
            }
            UpdateHostSnapshot(reason);
        }

        /// <summary>
        /// 2.11.8 (GitLab #15, plan finding FWP-E-04): while hosting with config sync on, the host reads the files it
        /// packed at launch (what its players received), written to ConfigPaths.OwnSnapshotDir, instead of its live
        /// files. Before, a host that edited a file after starting the game played the new file while its players
        /// played the launch-time one. Off again once this PC is not hosting with sync on.
        /// </summary>
        private static void UpdateHostSnapshot(string reason)
        {
            bool hostingSynced = CurrentRole() == Role.Host && (Lobby?.SyncEnabled ?? false) && _localEntries != null && _localHash != null;
            if (!hostingSynced)
            {
                if (!ConfigPaths.UseOwnSnapshot) return;
                ConfigPaths.UseOwnSnapshot = false;
                Plugin.Logger.LogInfo($"{Tag} Not hosting with config sync any more ({reason}): your own files apply again.");
                return;
            }
            if (ConfigPaths.UseOwnSnapshot) return;

            try
            {
                WriteOwnSnapshot();
                ConfigPaths.UseOwnSnapshot = true;
                Plugin.Logger.LogInfo($"{Tag} Hosting with config sync on ({reason}): you play the files you sent (hash {ConfigSyncCodec.ShortHash(_localHash)}, packed at launch), the same as your players. Folder: {ConfigPaths.OwnSnapshotDir}");
            }
            catch (Exception ex)
            {
                ConfigPaths.UseOwnSnapshot = false;
                Plugin.Logger.LogError($"{Tag} Could not write the host snapshot ({ex.Message}); you play your live files. If you edited them since the game started, your players have the launch-time version.");
            }
        }

        private static void WriteOwnSnapshot()
        {
            foreach (var id in ConfigSyncCodec.AllIds)
            {
                if (id == SyncFileId.GlobalMultipliers) continue; // in memory; the CFG is read once at launch
                string path = Path.Combine(ConfigPaths.OwnSnapshotDir, ConfigSyncCodec.RelativePathFor(id));
                if (_localEntries.TryGetValue(id, out var data)) AtomicFileWriter.WriteIfChanged(path, data);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        // ------------------------------------------------------------------
        // Lobby / session tracking
        // ------------------------------------------------------------------

        /// <summary>Called every frame from Config.Core.FrameDriver (2.11.6); does its work once per second.</summary>
        internal static void Tick()
        {
            if (!_initialized) return;
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextTick) return;
            _nextTick = now + TickSeconds;

            try
            {
                Role role = CurrentRole();
                if (role != _lastRole)
                {
                    Plugin.Logger.LogInfo($"{Tag} Lobby role: {DescribeRole(_lastRole)} -> {DescribeRole(role)}.");
                    if (_lastRole == Role.Client) LeaveClientRole(role);
                    if (_lastRole == Role.Host && ConfigPaths.UseOwnSnapshot)
                    {
                        ConfigPaths.UseOwnSnapshot = false;
                        Plugin.Logger.LogInfo($"{Tag} You are no longer hosting: your own files apply from the next match.");
                    }
                    if (role == Role.Client) _clientSince = now;
                    _lastRole = role;
                    Lobby?.System_RefreshHostState();
                    UpdateStatus();
                }

                // Left one lobby and joined another between two checks: treat as leave + join.
                if (role == Role.Client && _lastRole == Role.Client)
                {
                    string hostId = HostIdText();
                    if (_clientHostId != null && hostId != "unknown" && hostId != _clientHostId)
                    {
                        Plugin.Logger.LogInfo($"{Tag} Lobby owner changed ({_clientHostId} -> {hostId}).");
                        LeaveClientRole(Role.Client);
                        _clientSince = now;
                        UpdateStatus();
                    }
                    if (hostId != "unknown") _clientHostId = hostId;
                }

                if (role == Role.Client && !_nothingReceivedWarned && _hostSyncEnabled == null && _hostPackage == null
                    && _hostPackageError == null && now - _clientSince >= NothingReceivedSeconds)
                {
                    _nothingReceivedWarned = true;
                    Plugin.Logger.LogWarning($"{Tag} Nothing received from the host after {NothingReceivedSeconds:0} s. Your own configs apply. The host needs Crusader DE Tweaker 2.7.0 or later (with config sync on) for sync.");
                    UpdateStatus();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"{Tag} Lobby check failed: {ex.Message}");
            }
        }

        private static void LeaveClientRole(Role now)
        {
            Revert(now == Role.Host ? "you are now the lobby owner" : "you left the lobby or the match ended");
            _hostSyncEnabled = null;
            _hostPackage = null;
            _hostPackageError = null;
            _problem = null;
            _clientSince = -1f;
            _nothingReceivedWarned = false;
            _clientHostId = null;
            // The field held the host's value; put this player's own preference back.
            Lobby?.RestoreOwnSyncEnabled(_ownSyncEnabled);
            Lobby?.RestoreOwnMaxCounts(_ownMaxCounts);
            LobbyMaxCounts.SetLobbyValues(_ownMaxCounts, "your own (no longer in the host's lobby)");
        }

        private static void OnSessionStarting(string hook)
        {
            try
            {
                Role role = CurrentRole();
                if (role == Role.Client)
                {
                    if (_applied)
                        Plugin.Logger.LogInfo($"{Tag} {hook}: match starting with the host's configs (hash {ConfigSyncCodec.ShortHash(_appliedHash)}).");
                    else
                        Plugin.Logger.LogWarning($"{Tag} {hook}: match starting WITHOUT the host's configs ({ClientStateText()}). Your own configs apply.");
                    return;
                }

                if (role == Role.Host && (Lobby?.SyncEnabled ?? false))
                {
                    Plugin.Logger.LogInfo($"{Tag} {hook}: hosting with config sync ON; players received hash {ConfigSyncCodec.ShortHash(_localHash)}.");
                    WarnIfOwnFilesChanged();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"{Tag} {hook} check failed: {ex.Message}");
            }
        }

        /// <summary>Host only: clients got the launch-time files; say so if they were edited since.</summary>
        private static void WarnIfOwnFilesChanged()
        {
            if (_localHash == null) return;
            byte[] now = ConfigSyncCodec.ComputeBodyHash(ReadOwnEntries(out _));
            if (!ConfigSyncCodec.BytesEqual(now, _localHash))
                Plugin.Logger.LogWarning($"{Tag} Your config files changed since the game started (hash {ConfigSyncCodec.ShortHash(_localHash)} -> {ConfigSyncCodec.ShortHash(now)}). You and your players play the launch-time version (the one they received); restart the game to use and send the new one.");
        }

        // ------------------------------------------------------------------
        // Status line
        // ------------------------------------------------------------------

        private static void UpdateStatus()
        {
            if (Lobby == null) return;

            string ownPackage = _localError != null
                ? $"Your configs could not be packed: {_localError}"
                : _localHash == null ? string.Empty
                : $"Your configs: {_localFileCount} files, hash {ConfigSyncCodec.ShortHash(_localHash)}, v{PluginInfo.PLUGIN_VERSION}";

            switch (_lastRole)
            {
                case Role.Host:
                    Lobby.SetStatus(
                        Lobby.SyncEnabled
                            ? (_localError == null ? "Hosting: players who join use your configs." : "Hosting, but your configs could not be packed: players keep their own.")
                            : "Hosting with config sync OFF: every player uses their own configs.",
                        ownPackage);
                    break;

                case Role.Client:
                    if (_applied)
                    {
                        var p = _hostPackage;
                        string hostVersion = null;
                        if (p != null) ConfigSyncCodec.SplitWireVersion(p.HostModVersion, out hostVersion, out _);
                        string versions = hostVersion != null && hostVersion != PluginInfo.PLUGIN_VERSION
                            ? $"host v{hostVersion}, you v{PluginInfo.PLUGIN_VERSION}"
                            : $"v{PluginInfo.PLUGIN_VERSION}";
                        Lobby.SetStatus("Using the host's configs for this lobby.",
                            $"Hash {ConfigSyncCodec.ShortHash(_appliedHash)}, {p?.Entries.Count ?? 0} files, {versions}");
                    }
                    else
                    {
                        Lobby.SetStatus(ClientStatusLine(), ownPackage);
                    }
                    break;

                default:
                    Lobby.SetStatus("Not in a multiplayer lobby. Your own configs apply.", ownPackage);
                    break;
            }
        }

        private static string ClientStatusLine()
        {
            if (_problem != null) return _problem;
            if (_hostSyncEnabled == false) return "Host has config sync turned off: everyone uses their own configs.";
            if (_nothingReceivedWarned) return "Nothing received from the host. Your own configs apply. The host needs Crusader DE Tweaker 2.7.0+ with sync on.";
            return "Waiting for the host's configs...";
        }

        private static string ClientStateText()
        {
            if (_problem != null) return _problem;
            if (_hostSyncEnabled == false) return "host has sync off";
            if (_hostPackage == null) return "no valid package received";
            return "host sync state not received";
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Role CurrentRole()
        {
            try
            {
                if (!GameNetworkAPI.IsNetworkedEnvironment()) return Role.None;
                return GameNetworkAPI.IsLocalHost() ? Role.Host : Role.Client;
            }
            catch (Exception ex)
            {
                // Fail toward "not a client": never use someone else's configs on an unknown state.
                Plugin.Logger.LogWarning($"{Tag} Network state query failed: {ex.Message}");
                return Role.None;
            }
        }

        /// <summary>
        /// True while every PC of this lobby reads the host's Units / Structures files: this PC is a client that
        /// applied the host's package, or the host with sync on and a package to send. A client that rejected the
        /// package plays with its own files (and its own tables) and is out of sync anyway. Read from the
        /// simulation thread by UnitCapHandler (2.11.3, FWP-E-01).
        /// </summary>
        internal static bool AllPeersUseHostFiles
        {
            get
            {
                Role role = CurrentRole();
                if (role == Role.Client) return _applied && ConfigPaths.UseHostSyncFiles;
                if (role == Role.Host) return (Lobby?.SyncEnabled ?? false) && _localHash != null;
                return false;
            }
        }

        private static string DescribeRole(Role role) =>
            role == Role.Host ? "hosting" : role == Role.Client ? "multiplayer client" : "not in a lobby";

        private static string HostIdText()
        {
            try
            {
                return GameNetworkAPI.GetHostSteamId()?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
