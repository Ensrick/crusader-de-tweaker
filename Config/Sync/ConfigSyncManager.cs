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
//   Reconcile()                          host sync on + valid package -> ApplyHost(); off -> Revert().
//   Tick() (Plugin.Update, 1 s)          role changes; revert once no longer a multiplayer client.
//   OnStartMap / OnLoadSave (Pre)        backstop revert for non-client sessions; match-start log lines.
//
// IMPORTANT FOR AI AGENTS:
// - The player's own config files are NEVER written here. Host files go to ConfigPaths.HostSyncDir and
//   the loaders are redirected with ConfigPaths.UseHostSyncFiles; multipliers are overridden in memory
//   (BepInExConfigManager.ApplyHostMultipliers, autosave off).
// - Apply and revert both start with TemplateBaseline.RestoreAll() so the -1 sentinel cannot leak one
//   side's overrides into the other (design doc section 8). Keep the launch apply order:
//   Units TOML -> Structures TOML -> matrices -> multiplier template writes.
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
using CrusaderDETweaker.Config.DamageMatrix;
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

        // This player's own SyncEnabled preference (the ViewModel field holds the host's while a client).
        private static bool _ownSyncEnabled = true;

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
                LocalPackage = ConfigSyncCodec.Pack(PluginInfo.PLUGIN_VERSION, entries, out _localHash, out int bodyBytes);
                _localFileCount = entries.Count;
                _localError = null;
                Plugin.Logger.LogInfo($"{Tag} Packed your configs for hosting: {entries.Count} files, {rawBytes} bytes raw, {LocalPackage.Length} bytes on the wire, hash {ConfigSyncCodec.ShortHash(_localHash)}, v{PluginInfo.PLUGIN_VERSION}.");
            }
            catch (Exception ex)
            {
                // Clients then receive an empty package and report it; the local game is unaffected.
                LocalPackage = Array.Empty<byte>();
                _localHash = null;
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

            var verdict = ConfigSyncCodec.CompareVersions(package.HostModVersion, PluginInfo.PLUGIN_VERSION);
            if (verdict == ConfigSyncCodec.VersionVerdict.Incompatible)
            {
                Plugin.Logger.LogError($"{Tag} REJECTED the host's configs: host runs v{package.HostModVersion}, you run v{PluginInfo.PLUGIN_VERSION} (major.minor must match). Your own configs apply.");
                _problem = $"The host runs an incompatible Crusader DE Tweaker (v{package.HostModVersion}, you v{PluginInfo.PLUGIN_VERSION}). Your own configs apply.";
                Revert("the host's version is incompatible");
                return;
            }
            if (verdict == ConfigSyncCodec.VersionVerdict.PatchDiffers)
                Plugin.Logger.LogWarning($"{Tag} Host runs v{package.HostModVersion}, you run v{PluginInfo.PLUGIN_VERSION}: same major.minor, applying. Update to the same version to be safe.");

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

                var (restored, failed) = TemplateBaseline.RestoreAll();
                ConfigPaths.UseHostSyncFiles = true;

                // Same order as launch: multiplier values first (the template multipliers read them),
                // then Units TOML, Structures TOML, the 7 matrices, then the multiplier template writes.
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

                ApplyTemplatesFromActiveFiles();

                _applied = true;
                _appliedHash = package.BodyHash;
                _problem = null;
                Plugin.Logger.LogInfo($"{Tag} Applied the host's configs (hash {hash}, host v{package.HostModVersion}): {string.Join(", ", written)}; {multiplierNote}. Tables reset to game values first ({restored} cells{(failed > 0 ? $", {failed} FAILED" : "")}). GameplaySettings from the host apply at match start. Host files: {ConfigPaths.HostSyncDir}");
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

        private static void ApplyTemplatesFromActiveFiles()
        {
            ConfigLoader.ApplyAllUnitConfigs();
            ConfigLoader.ApplyAllStructureConfigs();
            DamageMatrixManager.LoadAll();
            BepInExConfigManager.ReapplyTemplateMultipliers();
        }

        /// <summary>Back to this player's own configs. No-op when the host's are not in use.</summary>
        private static void Revert(string reason)
        {
            if (!_applied && !ConfigPaths.UseHostSyncFiles && !BepInExConfigManager.HostOverrideActive) return;

            try
            {
                ConfigPaths.UseHostSyncFiles = false;
                BepInExConfigManager.RestoreOwnMultipliers();
                var (restored, failed) = TemplateBaseline.RestoreAll();
                ApplyTemplatesFromActiveFiles();
                Plugin.Logger.LogInfo($"{Tag} Reverted to your own configs ({reason}). Tables reset ({restored} cells{(failed > 0 ? $", {failed} FAILED" : "")}) and your files re-applied. Note: GameplaySettings keys your own file leaves at 'no override' may keep the host's value until you restart the game.");
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

        // ------------------------------------------------------------------
        // Lobby / session tracking
        // ------------------------------------------------------------------

        /// <summary>Called every frame from Plugin.Update; does its work once per second.</summary>
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

                // Any other session must run on this player's own configs.
                if (_applied || ConfigPaths.UseHostSyncFiles || BepInExConfigManager.HostOverrideActive)
                    Revert($"{hook}: a session that is not a multiplayer-client session is starting");

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
                Plugin.Logger.LogWarning($"{Tag} Your config files changed since the game started (hash {ConfigSyncCodec.ShortHash(_localHash)} -> {ConfigSyncCodec.ShortHash(now)}). Players received the launch-time version; restart the game to send the new one.");
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
                        string versions = p != null && p.HostModVersion != PluginInfo.PLUGIN_VERSION
                            ? $"host v{p.HostModVersion}, you v{PluginInfo.PLUGIN_VERSION}"
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
