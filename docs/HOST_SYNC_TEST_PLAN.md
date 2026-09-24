# Multiplayer host config sync - 2-machine test plan (v2.7.0)

Acceptance test for [HOST_SYNC_DESIGN.md](HOST_SYNC_DESIGN.md): two players on two PCs (long distance is
fine; that is the real use case). **H** = host, **C** = client (joins H's lobby). Every expected log line
below is quoted from the code (`Config/Sync/ConfigSyncManager.cs`); `<...>` marks values that vary. The log
is `{GameDir}\BepInEx\LogOutput.log`; it is overwritten at every launch, so copy it after each session
you want to keep.

Record for each step: PASS / FAIL, and for a FAIL the log lines around it (search `[ConfigSync]`).

---

## 0. Setup (both machines)

1. Script Extender 2.8.0 and Crusader DE Tweaker **2.7.0** installed on both (same version; check the
   `Loading [Crusader DE Tweaker 2.7.0]` line).
2. Make the two config sets different, so you can see whose values apply:

   | Machine | File | Change |
   |---|---|---|
   | H | `CrusaderDETweaker_Units.toml`, `[CHIMP_TYPE_KNIGHT]` | `GoldCost = 5000` |
   | H | `CrusaderDETweaker_GlobalMultipliers.cfg` | `LowWallCostMultiplier = 1` |
   | C | `CrusaderDETweaker_Units.toml`, `[CHIMP_TYPE_ARCHER]` | `GoldCost = 1` |
   | C | leave `[CHIMP_TYPE_KNIGHT] GoldCost = -1` | (so C's own Knight price is the game's) |

3. On **C only**, before starting the game, record the hashes of its own config files (PowerShell):

   ```powershell
   $cfg = 'C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker'
   Get-ChildItem $cfg -File -Recurse | Where-Object FullName -notlike '*\HostSync\*' |
       Get-FileHash | Select-Object Hash, Path | Out-File "$env:USERPROFILE\Desktop\cdt_before.txt"
   ```

   (Note: C's game rewrites the files once at launch when migrating them; if the hashes differ between
   "before launch" and "after launch but before joining", take the second snapshot as the reference:
   repeat the command after reaching the main menu.)

## 1. Launch (both)

Expected on both, in order:

```
 Applying unit / structure / damage-matrix settings from your files (launch).
[ConfigSync] Packed your configs for hosting: 11 files, <raw> bytes raw, <wire> bytes on the wire, hash <12 hex>, v2.7.0.
=== ALL 5 TEST SUITES PASSED ===
```

`<wire>` should be around 12,000-13,000 for default-sized files and must be under 200,000. Write down H's
hash (**H-hash**).

FAIL if: `Lobby tab NOT registered`, `Could not pack your configs`, or any `[FAIL]` test line.

## 2. H opens a lobby, C joins

1. H creates a multiplayer lobby. H's log: `[ConfigSync] Lobby role: not in a lobby -> hosting.`
2. H opens the lobby's Mod Options window, tab **Crusader DE Tweaker**: the checkbox "Players who join use
   my configs (host only)" is ticked and clickable; status "Hosting: players who join use your configs.",
   second line "Your configs: 11 files, hash <H-hash>, v2.7.0".
3. C joins. H's log shows SE's join push: `Syncing all mod settings to [<C's name>]`.
4. C's log, within a few seconds (order of the first lines may vary):

   ```
   [ConfigSync] Host config sync is ON (from lobby owner <H steam id>).
   [ConfigSync] Received the host's configs from lobby owner <H steam id>: <wire> bytes, 11 files, hash <H-hash>, host v2.7.0. Verified OK.
   [TemplateConfig] Applying unit / structure / damage-matrix settings from the lobby host's files (host sync applied).
   Applied unit configs: Units=<n>, Skipped=<n>, Errors=0
   Loading damage matrix: ...\CrusaderDETweaker\HostSync\DamageMatrices\CrusaderDETweaker_RangedDamage.csv
   ...
   [ConfigSync] Applied the host's configs (hash <H-hash>, host v2.7.0): CrusaderDETweaker_Units.toml, CrusaderDETweaker_Structures.toml, CrusaderDETweaker_GameplaySettings.toml, CrusaderDETweaker_MeleeDamage.csv, ... ; 15 multipliers. Tables were reset to game values first. GameplaySettings from the host apply at match start. Host files: ...\CrusaderDETweaker\HostSync
   [ConfigSync] Lobby role: not in a lobby -> multiplayer client.
   ```

   **The hash C logs must equal H-hash.** `<wire>` must equal H's `<wire>` from step 1.
5. C opens Mod Options, tab Crusader DE Tweaker: checkbox ticked but greyed out; status "Using the host's
   configs for this lobby.", second line "Hash <H-hash>, 11 files, v2.7.0".
6. C tries to click the checkbox anyway: nothing changes. (If the click goes through the grey-out, SE
   logs `rejected client edit of host-only property [SyncEnabled]; reverting UI` and the box snaps back.)
7. C checks the folder `...\config\CrusaderDETweaker\HostSync\`: 3 TOML files, `DamageMatrices\` with 7 CSVs,
   `CrusaderDETweaker_GlobalMultipliers.host.txt`. H's Knight line in the copied Units file reads
   `GoldCost = 5000`.

FAIL if: C shows "Waiting for the host's configs..." for more than a few seconds, `REJECTED`, `FAILED`, or
the hashes differ.

## 3. Host toggles sync in the lobby

1. H unticks the checkbox. H's log: `[ConfigSync] You turned host config sync OFF; players in your lobby are told now.`
   H's status: "Hosting with config sync OFF: every player uses their own configs."
2. C's log:

   ```
   [ConfigSync] Host config sync is OFF (from lobby owner <H steam id>).
   [TemplateConfig] Applying unit / structure / damage-matrix settings from your files (host sync ended: the host turned config sync off).
   [ConfigSync] Reverted to your own configs (the host turned config sync off). Tables reset to game values and your files re-applied. Note: ...
   ```
   C's status: "Host has config sync turned off: everyone uses their own configs."
3. H ticks it again. C's log: `Host config sync is ON ...` then `Applied the host's configs (hash <H-hash> ...)`.
   C's status back to "Using the host's configs for this lobby."

## 4. The match

1. H starts the match (leave sync ON).
2. H's log: `[ConfigSync] OnStartMap: hosting with config sync ON; players received hash <H-hash>.`
   C's log: `[ConfigSync] OnStartMap: match starting with the host's configs (hash <H-hash>).` followed by
   `[GlobalConfig] OnStartMap (Post) fired ...`.
   While the match loads, C's log may show more `[TemplateConfig] Applying ... from the lobby host's
   files (after SE map-unload reset)` / `(new game / map start)` lines: these must say **the lobby host's
   files**. A `from your files` line on C during the match is a FAIL.
3. Both players look at the Barracks:
   - **Knight** costs 5000 gold for **both** players (H's value).
   - **Archer** shows the **same** price for both players, and it is **not** 1 (C's own override must not
     apply while synced; H leaves it at `-1`).
4. Build a short stretch of low wall on both sides and compare the stone cost (H's
   `LowWallCostMultiplier = 1` applies to both).
5. Play at least 10 minutes with fighting (melee and ranged) on both sides. **No desync / out-of-sync
   message.** Note any.

FAIL if: `match starting WITHOUT the host's configs` on C, different prices between the two players, or a
desync.

## 5. After the match (C returns to the menu)

1. C's log, within about a second of leaving the match / lobby:

   ```
   [ConfigSync] Lobby role: multiplayer client -> not in a lobby.
   [TemplateConfig] Applying unit / structure / damage-matrix settings from your files (host sync ended: you left the lobby or the match ended).
   [ConfigSync] Reverted to your own configs (you left the lobby or the match ended). Tables reset to game values and your files re-applied. Note: GameplaySettings keys your own file leaves at 'no override' may keep the host's value until you restart the game.
   ```
   Alternative, equally a PASS: if a map-unload reset comes first (leaving a match unloads the map), the
   revert happens inside that re-apply instead:
   ```
   [ConfigSync] Reverted to your own configs (after SE map-unload reset: you are no longer a multiplayer client). Your files are re-applied now. ...
   [TemplateConfig] Applying unit / structure / damage-matrix settings from your files (after SE map-unload reset).
   ```
   If neither appears, the same line with `new game / map start` appears at the next single-player start
   (step 6). Record which one you saw: it answers open question 2 of the design doc.
2. C's Mod Options status: "Not in a multiplayer lobby. Your own configs apply."
3. On C, re-run the hash command from setup step 3 into `cdt_after.txt` and compare:

   ```powershell
   Compare-Object (Get-Content "$env:USERPROFILE\Desktop\cdt_before.txt") (Get-Content "$env:USERPROFILE\Desktop\cdt_after.txt")
   ```
   **No output = PASS** (C's own files, including `CrusaderDETweaker_GlobalMultipliers.cfg`, were not
   changed).

## 6. Single player on C after the match (same game session, no restart)

1. C starts a skirmish.
2. Knight costs the game's normal price (C's own file has `-1`), **not** 5000.
3. Archer costs **1** (C's own override is back).
4. Low wall stone cost is C's own (C did not change `LowWallCostMultiplier`).

FAIL if any of H's values is still active.

## 7. Host edited files after launch (optional)

1. H edits any value in its Units file while the game is running, then starts a new lobby match with C.
2. H's log at match start: `[ConfigSync] Your config files changed since the game started (hash <H-hash> -> <new>). Players received the launch-time version; restart the game to send the new one.`
3. C still logs `Applied the host's configs (hash <H-hash>...)` (the launch-time package).

## 8. Version mismatch (optional, only if a 2.6.6 install is at hand)

1. H runs 2.6.6 (no sync), C runs 2.7.0. C joins.
2. After 15 s C's log: `[ConfigSync] Nothing received from the host after 15 s. Your own configs apply. ...`
   and the status "Nothing received from the host. ...".

---

## What to send back

- PASS / FAIL per step (1-6, plus 7/8 if run).
- Both `LogOutput.log` files from the session (H and C), or at least every `[ConfigSync]` line and the
  lines around any FAIL.
- For step 5: which revert line appeared (role change or single-player start).
- Any desync message and roughly when it happened.
