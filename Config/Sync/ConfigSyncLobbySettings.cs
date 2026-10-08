// Config/Sync/ConfigSyncLobbySettings.cs
//
// PURPOSE: The "Crusader DE Tweaker" tab in SHCDE-SE's lobby Mod Options hub (SE 2.8.0 lobby mod
//          settings). Backing XAML: Override/ScriptExtenderUI/CDTLobbySettings.xaml.
//          Design: docs/HOST_SYNC_DESIGN.md.
//
// SYNC MODEL (SE 2.8.0, sources in the design doc section 4):
// - SyncEnabled    [SyncHostOnly]                 host's switch; persisted as the host's own preference.
// - HostConfigBlob [SyncHostOnly, DoNotPersist]   the host's package (ConfigSyncCodec). The getter always
//                                                 returns THIS machine's package: SE reads it only on the
//                                                 host (join push). The setter only receives the host's.
// - Status         (no attribute)                 local UI text; SE ignores it entirely.
// - MaxCounts      [SyncHostOnly]                 lobby MaxCount values, "KEY=V;KEY=V" (LobbyMaxCounts, GitLab #5);
//                                                 persisted as the host's own; applied on every peer.
// - UnitMaxCounts / BuildingMaxCounts             UI-only rows behind the MaxCount boxes; an edit rebuilds
//                                                 MaxCounts (host / single player) or is reverted (client).
//
// IMPORTANT FOR AI AGENTS:
// - Every [SyncHostOnly] setter calls CanEdit() first (SE's ownership gate). A setter call that passes
//   it while we are a networked client is the lobby owner's verified value (SE opens an authorised-
//   write window only for that), which is why the manager can trust it.
// - The received blob is NOT stored in the getter's field, so a client's own package can never be
//   mistaken for the host's.
// - The mod name used at registration is the packet key; keep it PluginInfo.PLUGIN_NAME everywhere.
//
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;

namespace CrusaderDETweaker.Config.Sync
{
    /// <summary>One unit / building line of the MaxCount list in the lobby tab. UI only: never synced itself.</summary>
    public sealed class LobbyMaxCountRow : INotifyPropertyChanged
    {
        private readonly Action<LobbyMaxCountRow, string> _edited;
        private string _text = string.Empty;

        internal LobbyMaxCountRow(string key, Action<LobbyMaxCountRow, string> edited)
        {
            Key = key;
            Label = LobbyMaxCounts.Label(key);
            _edited = edited;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Config key, e.g. CHIMP_TYPE_KNIGHT.</summary>
        public string Key { get; }

        /// <summary>Shown name, e.g. Knight.</summary>
        public string Label { get; }

        /// <summary>The box: "" = use the file, -1 = unlimited, 0 = not allowed, N = at most N.</summary>
        public string Text
        {
            get => _text;
            set => _edited(this, value);
        }

        internal void Show(string text)
        {
            if (string.Equals(_text, text, StringComparison.Ordinal)) return;
            _text = text;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }

        /// <summary>Snap the box back to the value in effect (rejected edit).</summary>
        internal void Revert() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
    }

    /// <summary>Lobby ViewModel for multiplayer host config sync. See the file header.</summary>
    public class ConfigSyncLobbySettings : LobbyModSettingsBaseViewModel
    {
        private bool _syncEnabled = true;
        private string _status = "Not in a lobby.";
        private string _details = string.Empty;
        private string _maxCounts = string.Empty;

        public ConfigSyncLobbySettings()
        {
            UnitMaxCounts = new ObservableCollection<LobbyMaxCountRow>(LobbyMaxCounts.UnitTypes.Select(u => new LobbyMaxCountRow(u.ToString(), OnRowEdited)));
            BuildingMaxCounts = new ObservableCollection<LobbyMaxCountRow>(LobbyMaxCounts.BuildingTypes.Select(b => new LobbyMaxCountRow(b.ToString(), OnRowEdited)));
        }

        /// <summary>Unit MaxCount boxes (recruitable units, then siege engines). UI only.</summary>
        public ObservableCollection<LobbyMaxCountRow> UnitMaxCounts { get; }

        /// <summary>Building MaxCount boxes (every Structures file type but the destroyed towers). UI only.</summary>
        public ObservableCollection<LobbyMaxCountRow> BuildingMaxCounts { get; }

        /// <summary>
        /// Every MaxCount value set in the lobby, "CHIMP_TYPE_KNIGHT=10;STRUCT_STABLES=2" (sorted; empty = none).
        /// Host only: the host's string is what every player in the lobby uses (GitLab #5).
        /// </summary>
        [SyncHostOnly]
        public string MaxCounts
        {
            get => _maxCounts;
            set
            {
                if (!CanEdit()) return;
                string normalized = LobbyMaxCounts.Encode(LobbyMaxCounts.Parse(value, null));
                bool changed = !string.Equals(_maxCounts, normalized, StringComparison.Ordinal);
                _maxCounts = normalized;
                ShowMaxCounts();
                if (changed) OnPropertyChanged(nameof(MaxCounts));
                ConfigSyncManager.OnMaxCountsSet(value, normalized);
            }
        }

        private void OnRowEdited(LobbyMaxCountRow row, string text)
        {
            if (!LobbyMaxCounts.TryParseValue(text, out int? value) || !CanEdit(nameof(MaxCounts)))
            {
                row.Revert();
                return;
            }
            var values = LobbyMaxCounts.Parse(_maxCounts, null);
            if (value.HasValue) values[row.Key] = value.Value;
            else values.Remove(row.Key);
            MaxCounts = LobbyMaxCounts.Encode(values);
            row.Revert();   // show the normalised value (" 05" -> "5")
        }

        private void ShowMaxCounts()
        {
            var values = LobbyMaxCounts.Parse(_maxCounts, null);
            foreach (var row in UnitMaxCounts.Concat(BuildingMaxCounts))
                row.Show(values.TryGetValue(row.Key, out int v) ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>Put this player's own lobby MaxCount values back (left a lobby where the host's applied).</summary>
        internal void RestoreOwnMaxCounts(string own)
        {
            own = own ?? string.Empty;
            if (string.Equals(_maxCounts, own, StringComparison.Ordinal)) return;
            _maxCounts = own;
            ShowMaxCounts();
            OnPropertyChanged(nameof(MaxCounts));
        }

        /// <summary>
        /// Host switch: on = joining players use the host's Crusader DE Tweaker configs for this lobby's
        /// matches; off = everyone plays on their own files.
        /// </summary>
        [SyncHostOnly]
        public bool SyncEnabled
        {
            get => _syncEnabled;
            set
            {
                if (!CanEdit()) return;
                bool changed = _syncEnabled != value;
                _syncEnabled = value;
                if (changed) OnPropertyChanged(nameof(SyncEnabled));
                ConfigSyncManager.OnSyncEnabledSet(value);
            }
        }

        /// <summary>
        /// This machine's package (built at launch). On a client the verified host package arrives
        /// through the setter and is handed to the manager, never stored here.
        /// </summary>
        [SyncHostOnly]
        [DoNotPersist]
        public byte[] HostConfigBlob
        {
            get => ConfigSyncManager.LocalPackage ?? Array.Empty<byte>();
            set
            {
                if (!CanEdit()) return;
                ConfigSyncManager.OnHostBlobSet(value);
            }
        }

        /// <summary>One-line state shown in the panel. Local only.</summary>
        public string Status => _status;

        /// <summary>Second line: hash / file count / versions. Local only.</summary>
        public string Details => _details;

        internal void SetStatus(string status, string details)
        {
            if (!string.Equals(_status, status, StringComparison.Ordinal))
            {
                _status = status;
                OnPropertyChanged(nameof(Status));
            }
            details = details ?? string.Empty;
            if (!string.Equals(_details, details, StringComparison.Ordinal))
            {
                _details = details;
                OnPropertyChanged(nameof(Details));
            }
        }

        /// <summary>
        /// Put this player's own preference back after leaving a lobby where the field held the host's
        /// value. Only called when not a multiplayer client (so the change is locally authoritative).
        /// </summary>
        internal void RestoreOwnSyncEnabled(bool own)
        {
            if (_syncEnabled == own) return;
            _syncEnabled = own;
            OnPropertyChanged(nameof(SyncEnabled));
        }
    }
}
