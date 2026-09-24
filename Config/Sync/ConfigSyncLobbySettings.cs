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
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;

namespace CrusaderDETweaker.Config.Sync
{
    /// <summary>Lobby ViewModel for multiplayer host config sync. See the file header.</summary>
    public class ConfigSyncLobbySettings : LobbyModSettingsBaseViewModel
    {
        private bool _syncEnabled = true;
        private string _status = "Not in a lobby.";
        private string _details = string.Empty;

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
