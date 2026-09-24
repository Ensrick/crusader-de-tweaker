# Multiplayer host config sync - design (v2.7.0)

Status: implemented in 2.7.0, **not yet tested in a real multiplayer match**. Acceptance test:
[HOST_SYNC_TEST_PLAN.md](HOST_SYNC_TEST_PLAN.md).

All Script Extender (SE) line references are to SHCDE-SE **2.8.0** (`shcde-script-extender` fork,
`main` = `5b4d48e chore(release): 2.8.0`, the build installed in the game: `SHCDESE.dll` 2.8.0+9fbe32f).
Paths are relative to `src/SHCDESE.BepInEx/`. Game references are to the decompiled
`Assembly-CSharp.dll` (`Platform_Multiplayer`) and `com.rlabrecque.steamworks.net.dll` shipped with the
game.

---

## 1. Goals and non-goals

**Goals**

1. When a player joins a multiplayer lobby, the lobby **host's** balance configs are sent to them and
   used for that session, so every machine simulates with the same numbers. Today players copy the
   config folder by hand before a match.
2. A client's own config files are **never** modified. The host's files live in a separate folder and
   the loaders are redirected to it.
3. When the session ends (or the host turns sync off), the client is back on exactly its own
   configuration, without a game restart.
4. The host can turn the feature off in the lobby's mod-settings tab. Clients see the state but cannot
   change it.
5. Every step is logged with a `[ConfigSync]` prefix, readable by a player filing a bug report.
6. Single-player and skirmish are unaffected.

**Non-goals**

- Anti-cheat. A modified client can ignore the host's values locally; SE says the same of its lobby
  settings ("This is not anti-cheat", `docs/guides/mod-lobby-settings.md`). This is a coordination
  feature, like sharing the folder.
- Live editing. The host's package is built once, at game launch, from the files as they are then (the
  same rule as today: TOML/CSV edits need a restart).
- Syncing other mods' configs or the SE's own settings.
- Syncing in the middle of a running match (see 4.5: SE drops host-only packets once the lobby is gone).

## 2. What is synced

| Id | Content | Source on the host | Where the client puts it |
|---:|---|---|---|
| 1 | Units TOML | `CrusaderDETweaker_Units.toml` | `HostSync\CrusaderDETweaker_Units.toml` |
| 2 | Structures TOML | `CrusaderDETweaker_Structures.toml` | `HostSync\CrusaderDETweaker_Structures.toml` |
| 3 | GameplaySettings TOML | `CrusaderDETweaker_GameplaySettings.toml` | `HostSync\CrusaderDETweaker_GameplaySettings.toml` |
| 10-16 | The 7 damage matrices | `DamageMatrices\CrusaderDETweaker_*.csv` | `HostSync\DamageMatrices\` (same names) |
| 20 | Global multipliers | the **values** of the `[Multipliers]` section of `CrusaderDETweaker_GlobalMultipliers.cfg` | applied in memory; a copy is written to `HostSync\CrusaderDETweaker_GlobalMultipliers.host.txt` for inspection |

`HostSync\` is `{GameDir}\BepInEx\config\CrusaderDETweaker\HostSync\`.

**GlobalMultipliers.cfg: included (values only).** Decision and reasons:

- Every entry in `[Multipliers]` changes the simulation (damage-taken, health, fire, heal and wall-cost
  multipliers are read by the damage hooks at hit time or written into the unit/building tables).
  Leaving them out would keep a guaranteed source of desync, and players who share configs today
  copy this file too.
- The file itself is **not** copied or written. BepInEx owns it through a `ConfigFile`; the client's
  entries get the host's values **in memory** for the session with `ConfigFile.SaveOnConfigSet`
  switched off, and their own values are put back before it is switched on again, so the file on disk
  is never rewritten. `[Debug] DebugLogging` is local and not synced.
- The host sends `Key=value` lines (invariant culture, round-trip format), not the file text, so the
  client never parses a foreign BepInEx file and only keys it has bound itself are accepted.

## 3. Data flow

```
HOST (game launch, after ConfigManager + BepInExConfigManager init)
  read the 10 files + multiplier values
  -> ConfigSyncCodec.Pack        (wire format v1, SHA-256 of the body, gzip)
  -> ConfigSyncLobbySettings.HostConfigBlob   (getter; [SyncHostOnly, DoNotPersist])
       log: [ConfigSync] Packed N files (X bytes raw, Y bytes on the wire), hash H, v2.7.0

TRANSPORT (SE, unchanged)
  new lobby member seen by the game -> Platform_Multiplayer.SendCustomInfoToMember
  -> GameNetworkAPI.HandleSendCustomInfoToMember (GameNetworkAPI.cs:775-781)
  -> GameXAMLManagerAPI.SyncSettingsToNewPlayer (GameXAMLManagerAPI.cs:696-756): host only, every
     synced property read through its getter and sent with SendPacketToSteamId (reliable, channel 2)
  host toggles SyncEnabled -> PropertyChanged -> BroadcastSettingChange -> SendPacketToAllLobby

CLIENT
  lobby receive hook (ManagedHooks/Platform_Multiplayer_Hooks.cs:18-52) -> ReceiveSettingsUpdate
  -> ApplyHostOnlyUpdate (GameXAMLManagerAPI.cs:638-681): sender must be the Steam lobby owner,
     then the setter runs inside SE's authorised-write window
  -> ConfigSyncLobbySettings setter -> ConfigSyncManager.OnHostValueReceived
  -> when both the host's SyncEnabled (true) and blob are in:
       ConfigSyncCodec.TryUnpack      (size caps, magic, format version, bounded gunzip, SHA-256,
                                       fixed id whitelist, no duplicates, no trailing bytes)
       version rule (section 7)
       write HostSync\ files (AtomicFileWriter), remove a stale HostSync file the package lacks
       TemplateBaseline.RestoreAll    (tables back to the game's own values, see section 8)
       ConfigPaths.UseHostSyncFiles = true
       re-run the init apply order: Units TOML, Structures TOML, 7 matrices,
       multiplier override + wall-cost / fire / heal template multipliers
       log: [ConfigSync] Applied host config ...
  match start: OnStartMap/OnLoadMap/OnLoadSave (Post) read GameplaySettings through ConfigPaths,
       i.e. the host's file

REVERT (client)
  trigger: host turns sync off | no longer a networked client (checked every second) |
           a non-client session starts (OnStartMap / OnLoadSave Pre backstop)
  ConfigPaths.UseHostSyncFiles = false; own multiplier values back, SaveOnConfigSet restored
  TemplateBaseline.RestoreAll; re-run the init apply order from the client's own files
  log: [ConfigSync] Reverted to your own configs (reason)
```

## 4. The SE 2.8.0 lobby-settings contract this relies on (with sources)

### 4.1 Who may set a value

- `[SyncHostOnly]` setters call `CanEdit()` first (`SetSynced` does it). It returns false for a client
  in a networked session unless SE has opened its authorised-write window
  (`ViewModels/LobbyModSettingsBaseViewModel.cs:111-136`, window 46-56). A client's own click is
  rejected and the bound control snaps back.
- SE also refuses to broadcast or persist an unauthorised change
  (`API/GameXAMLManagerAPI.cs:248-252`, again in `BroadcastSettingChange` 380-385).

### 4.2 Whose value is accepted

`ApplyHostOnlyUpdate` (`API/GameXAMLManagerAPI.cs:638-681`): the host ignores host-only packets
(640-644); a client accepts one only when the transport sender equals
`SteamMatchmaking.GetLobbyOwner` (`GameNetworkAPI.GetHostSteamId`, `API/GameNetworkAPI.cs:1188-1196`),
fails closed when either is unknown (650-654), then writes through the setter inside
`BeginAuthorisedUpdate/EndAuthorisedUpdate` (668-676). The sender identity comes from Steam
(`m_identityPeer`, `ManagedHooks/Platform_Multiplayer_Hooks.cs:34`), never from the packet.

Consequence for this mod: **any setter call that passes `CanEdit()` while we are a networked client is
the verified host's value** (a local edit cannot pass, and restored settings are loaded at startup
when there is no network). `ConfigSyncManager` relies on exactly that, and logs the lobby owner's
Steam id that SE checked it against.

### 4.3 When values are pushed

- **Join / late join**: the host pushes every synced property to each new member
  (`SyncSettingsToNewPlayer`, `API/GameXAMLManagerAPI.cs:696-756`), called from the game's
  `SendCustomInfoToMember` when a member is first seen (`API/GameNetworkAPI.cs:775-781`). Values are
  read through the **getter** (723), so the blob does not have to be "changed" to be sent. Properties
  are sent in reflection order; the client code does not depend on the order.
- **Host change mid-lobby**: a `PropertyChanged` from the host is broadcast to all lobby members
  (`BroadcastSettingChange`, 350-421; lobby path `GameNetworkAPI.SendPacketToAllLobby`,
  `API/GameNetworkAPI.cs:966-1017`, reliable on channel 2). Toggling `SyncEnabled` therefore reaches
  every client; a client re-applies or reverts accordingly. The blob itself never changes after launch.
- A null getter value is skipped with a warning (`GameXAMLManagerAPI.cs:723-728`); the blob getter
  therefore returns an empty array rather than null, which the client reports as "empty package".

### 4.4 Persistence

Synced values are persisted by default. The blob is marked `[DoNotPersist]` (routing:
`API/Components/ModManager/LobbyModSettingsRouting.cs:31-33`) so ~12 KB of config is not written to
`LobbyModSettings\*.msgpack` on every change. `SyncEnabled` is persisted: it is the host's own
preference, stored only while locally owned (`LobbyModSettingsStorage.cs:128-139`) and restored during
registration before the view binds (`GameXAMLManagerAPI.cs:192-196`, storage `Load` 199-232). The file is
`BepInEx\plugins\CrusaderDETweaker\LobbyModSettings\Crusader DE Tweaker.msgpack`, written by SE.

While a client, the ViewModel holds the host's `SyncEnabled`. The client's own preference is remembered
by the manager and put back when it leaves (so hosting later starts from the player's own choice).

### 4.5 Transport and size limit

| Limit | Value | Source |
|---|---|---|
| Steam reliable message | 524,288 bytes | `Steamworks.Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend` in the game's `com.rlabrecque.steamworks.net.dll`; SE sends with `SteamNetworkingSend.Reliable` (`API/GameNetworkAPI.cs:1004, 1061`) |
| Game packet framing | 6-byte header, int32 length | `Platform_Multiplayer.MPData.ToBytes/FromBytes` (decompiled) |
| Largest message the game itself sends | 250,000 bytes | `Platform_Multiplayer` map transfer chunks (`Math.Min(250000, ...)`, decompiled) |
| **This mod's wire cap** | **200,000 bytes** | `ConfigSyncCodec.MaxWireBytes` |

Measured on a real config set (2.6.6 defaults plus edits): 140,649 bytes raw, ~11.6 KB compressed, so
the cap leaves more than 15x headroom and stays below the largest message the game already sends. The SE
envelope around the blob (`LobbyModSettingSyncPacket`: mod name, property name, assembly-qualified type
name, player id, MessagePack framing) adds a few hundred bytes. The host refuses to publish a package
above the cap and logs why (the client then sees "empty package").

The lockstep "chore" transport and its 1,200-byte cap (`API/GameNetworkAPI.cs:113`) are **not** used by
lobby settings.

**In-match**: `GetHostSteamId` returns null when there is no active lobby, and host-only packets are then
dropped (fails closed, 4.2). The package is therefore effectively frozen for a match; nothing here needs
mid-match updates. Note: SE's guide links a "Transport Limitations" section
(`docs/guides/mod-lobby-settings.md:43`) that does not exist in 2.8.0; the statements above come from
the code, not that section.

### 4.6 Registration and UI

- `GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(this, "Crusader DE Tweaker", vm,
  "ScriptExtenderUI/CDTLobbySettings.xaml")` in `Plugin.Awake()` (guide: "during your plugin's
  Awake()"). The mod name is the packet key and must be identical on every peer, so it is the constant
  `PluginInfo.PLUGIN_NAME`, never localized or versioned.
- XAML: a `Grid > ScrollViewer > StackPanel` panel like SE's template
  (`SHCDESE.ExampleMod/.../ModSettingsTemplate.xaml`), host-only control bound to `IsEnabled="{Binding
  IsHost}"` (cosmetic; the setter gate is the enforcement), tooltip with
  `Style="{DynamicResource SE_ToolTip_Scale}"` (2.8.0 guide: lobby views load before the global
  dictionaries, so `DynamicResource`).
- `IsHost` has no change notification; the hub refreshes it on every window open
  (`ViewModels/LobbyModSettingsHubViewModel.cs:62-71, 95`). The manager also calls
  `System_RefreshHostState()` when the lobby role changes.
- Optional sidebar banners (`Override/Assets/GUI/Sprites/CrusaderDETweaker_option_banner*.png`,
  added in SE da26389) are not shipped; the tab shows the name only.

## 5. Wire format (version 1)

All integers little-endian (`BinaryWriter`).

```
header (not compressed)
  4   magic                 "CDTS"
  2   format version        uint16 = 1
  1+n host mod version      BinaryWriter string (UTF-8, length-prefixed), 1..32 bytes, [0-9A-Za-z.+-]
  4   body length           int32, uncompressed
  32  body SHA-256
  ... gzip(body)
body
  2   entry count           uint16, 1..(number of known ids)
  per entry
    2 id                    uint16, one of section 2's ids
    4 length                int32, 1..MaxEntryBytes
    n bytes                 exact file bytes (TOML/CSV) or multiplier lines (id 20)
```

Limits: wire <= 200,000 bytes; body <= 4 MiB; one entry <= 1 MiB (the largest real file, the melee
matrix, is ~71 KB). Decompression reads at most `body length + 1` bytes, so a gzip bomb stops at the
declared size. The hash is over the uncompressed body, so it is independent of the gzip implementation
and identifies the content: host and client log the same 12-hex-digit prefix.

## 6. Security

- **Only the host's value is accepted**: enforced by SE (4.1, 4.2). The manager additionally ignores
  anything that arrives while it is not a networked client.
- **Size caps before any work**: wire size, declared body size, per-entry size, entry count, version
  string length, all checked before allocation or decompression.
- **No path traversal**: the package carries numeric ids, never names or paths. Each id maps to a fixed
  file name compiled into the mod (`ConfigSyncCodec.RelativePathFor`), unknown and duplicate ids reject
  the whole package. The on-load tests assert every mapped path is relative, has no `..` and no rooted
  or drive component.
- **Integrity**: SHA-256 over the body; any mismatch, truncation or trailing byte rejects the package.
- **Multiplier values**: keys must match `[A-Za-z0-9_]{1,64}`, no duplicates, values must be finite and
  >= 0 (the same floor `BepInExConfigHelper.ValidateMultiplier` enforces locally); only keys the client
  has bound itself are applied.
- **Content**: TOML/CSV text goes through the same loaders as local files (unknown keys/headers are
  skipped and logged, `-1` skips). A host can choose absurd stats, exactly as a shared folder could.
- **All-or-nothing**: a package is validated completely before anything is written or applied.

## 7. Versioning

| Host vs client | Rule | Client sees |
|---|---|---|
| Wire format version differs | reject | "Host runs an incompatible Crusader DE Tweaker (vX)" |
| Mod major.minor differs (2.7 vs 2.8) | reject | same message, both versions logged |
| Only the patch differs (2.7.0 vs 2.7.1) | apply, warn | "Using the host's configs (host v2.7.1, you v2.7.0)" |
| Host runs < 2.7.0 or no CDT | nothing arrives | after 15 s: "Nothing received from the host ..." |

Why major.minor: the loaders tolerate unknown keys and headers, so a patch difference cannot corrupt the
apply, while a minor release may change what a key means. Rejecting means both players use their own
files, which is today's behaviour, and the status line says why.

## 8. Reverting exactly: the template baseline

The obvious approach (re-run the loaders on the host's files, later on the client's own) is wrong
because of the `-1` sentinel ("leave the game value unchanged"):

- apply: where the host has `-1` and the client has an override, the client's own override would stay
  in the table while the host plays the game value -> the two machines differ;
- revert: where the host overrode a value that the client leaves at `-1`, the host's value would stay
  until a restart.

`Config/Core/TemplateBaseline` fixes both. Every template write the mod makes (property handlers, the 7
matrix loaders, wall-cost and fire/heal multipliers) first reports the cell it is about to change. The
first time a cell is seen, its current value is read and remembered; the first time is at launch, before
the mod has touched it, so what is remembered is the game's own value. `RestoreAll()` writes those back
(newest first). Apply = restore + host files; revert = restore + own files. Both end in exactly the state
the owner of the files would have after a fresh launch.

Unit/building caps (`MaxCount`) are dictionaries rebuilt by every Units/Structures apply, so they follow
automatically. Runtime multipliers are read at hit time from the `ConfigEntry` values (section 2).

Fix made on the way: `RangedDamageMatrixLoader` multiplied the CSV value by `RangedDamageTakenMultiplier`
when applying. At launch that code never ran (the multipliers are bound after the matrices load), and the
runtime hook applies the same multiplier at hit time, so any re-apply would have scaled ranged damage
twice. The apply-time scaling is removed; launch behaviour is unchanged.

## 9. GameplaySettings and the session hooks

GameplaySettings (siege stones, trade prices, gameplay options, auto-trade, disease, peasants) is
session state and is only written from the `OnStartMap` / `OnLoadMap` / `OnLoadSave` **Post** hooks
(`ConfigLoader.RegisterSessionHooks`), never at init or in the lobby. Those hooks read
`ConfigPaths.Globals`, which resolves into `HostSync\` while the host's configs are in use, so the
host's file applies at match start with no change to the hooks.

Revert cannot restore session state (there is no session in the menu, and writing it there is the
documented ACCESS_VIOLATION hazard). The client's next session applies the client's own file as usual.
**Known limitation**: a GameplaySettings key that the client's own file leaves at "no override"
(a `false` gameplay option, a `0` trade price) is not actively reset by the mod; whether the game resets
it at the next session start is not verified per key. The revert log line says so, and a game restart
always gives a clean state.

The client's `OnStartMap` / `OnLoadSave` **Pre** hooks run the backstop revert for any session that is not
a multiplayer-client session (so single player after a match can never read the host's files), and for
a client session they log whether the match starts with the host's configs.

## 10. Failure modes

| Situation | Log (client unless noted) | Lobby status line | Result |
|---|---|---|---|
| Host has sync off | `Host has config sync OFF` | "Host has config sync turned off: everyone uses their own configs." | own configs |
| Host runs < 2.7.0 / no CDT / nothing arrives in 15 s | `Nothing received from the host after 15 s` (warning) | "Nothing received from the host. Your own configs apply. The host needs Crusader DE Tweaker 2.7.0+ with sync on." | own configs |
| Package fails validation (size, magic, hash, id, ...) | `REJECTED host config: <reason>` (error) | "Host's configs were rejected (<reason>). Your own configs apply. See LogOutput.log." | own configs |
| Version rule rejects | `REJECTED host config: host v.. / you v..` (error) | "Host runs an incompatible Crusader DE Tweaker (v..). Your own configs apply." | own configs |
| Writing HostSync files or applying throws | `Applying the host's config FAILED` + exception (error), then revert | "Applying the host's configs failed. Your own configs apply. See LogOutput.log." | own configs |
| Host's package too big at launch (host) | `Package too large` (error, host) | host: "Your configs are too large to sync ..." | clients get an empty package -> rejected |
| Host edited config files after launch (host) | `Your config files changed since launch` (warning, host, at match start) | - | clients got the launch-time files; restart to resend |
| Match starts before the package was applied | `Match starting WITHOUT the host's configs: <state>` (warning) | - | own configs for that match |
| Lobby owner changes (host migration) | if we become owner: revert, `no longer a multiplayer client` | host status | we keep playing on our own configs; other members keep what they had |
| Game or connection lost mid-session | revert on the next check once the lobby/game is gone | - | own configs |

Every rejected package leaves the client's configuration exactly as it was.

## 11. Single player, skirmish and hosting

The manager acts only on values received while `GameNetworkAPI.IsNetworkedEnvironment() &&
!GameNetworkAPI.IsLocalHost()` (`API/GameNetworkAPI.cs:1103-1111, 1168-1182`). In single player and as a
host this is false, so nothing is received, redirected or reverted, and the host always plays on its own
files. Skirmish "members" are local AI (`SendPacketToAllLobby` skips `SkirmishMember`, 995), and no remote
lobby owner exists to send a host-only value.

## 12. Code map

| Piece | File |
|---|---|
| Wire format, validation, hash, version rule, multiplier text (pure, tested) | `Config/Sync/ConfigSyncCodec.cs` |
| Lobby ViewModel (`SyncEnabled`, `HostConfigBlob`, `Status`) | `Config/Sync/ConfigSyncLobbySettings.cs` |
| Orchestration: pack, receive, apply, revert, status, checks | `Config/Sync/ConfigSyncManager.cs` |
| First-write baseline and restore | `Config/Core/TemplateBaseline.cs` |
| Path redirect | `Config/Toml/ConfigPaths.cs` (`UseHostSyncFiles`), `Config/DamageMatrix/Core/MatrixPaths.cs` |
| Multiplier values + in-memory override | `Config/BepInEx/BepInExConfigManager.cs` |
| Lobby panel | `Override/ScriptExtenderUI/CDTLobbySettings.xaml` |
| Tests | `Tests/ConfigSyncTest.cs` (suite "ConfigSync") |

## 13. Open questions (need a real match to answer)

1. Order and timing of the join push relative to the client's lobby UI in a long-distance session.
2. Whether `gameMembers` / `activeLobby` are cleared promptly when a match ends (the revert check relies
   on them; the `OnStartMap`/`OnLoadSave` Pre backstop covers the next single-player session either way).
3. Whether template writes in the lobby behave like the ones at launch (the mod has only ever written
   templates at launch; the prototype this replaces assumed so, untested).
