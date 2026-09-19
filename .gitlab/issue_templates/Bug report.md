<!--
Thanks for reporting. A report WITH the log file attached can usually be answered within a day.
A report without one usually cannot be investigated at all, so please fill in every section.

Before you fill this in: reproduce the problem once, then quit the game and copy
BepInEx\LogOutput.log out of the game folder. The log is overwritten on every launch.
-->

### Versions

- **Mod version:** <!-- BepInEx\plugins\CrusaderDETweaker\info.json, or the log line "Loading [Crusader DE Tweaker x.y.z]" -->
- **Script Extender version:** <!-- BepInEx\plugins\000shcdese\info.json, or the log line "Loading [SHCDE-SE x.y.z]" -->
- **Game version:** <!-- bottom of the main menu, e.g. V2.8.2 -->

### Game mode where it happens

<!-- Delete the ones that do not apply. The mod applies settings at different moments in each mode, so this matters. -->
- New skirmish game
- Loaded save game
- Trail / campaign mission
- Multiplayer
- Map editor

### What you changed

<!-- File name plus the exact lines you edited, copied from the file. -->
```toml
CrusaderDETweaker_GameplaySettings.toml
["Trade Prices".STORED_BOWS]
BuyPrice = 52  # default: 155
```

### Steps to reproduce

<!-- What you did in the game, in order, after launching it. -->
1. Started a new skirmish (Custom Skirmish, any map).
2. Built a Trade Post and opened the market.
3. Looked at the buy price of Bows.

### What you expected, and what happened instead

- Expected:
- Actual:

### Attachments (both required)

<!-- Drag the files into this text box; GitLab uploads them and inserts a link. -->
- [ ] `BepInEx\LogOutput.log` from a session that shows the problem
- [ ] The config file I edited (the whole file, not a snippet)

### Other mods installed

<!-- List the folders in BepInEx\plugins. -->

### Anything else

<!-- Screenshots, saves, or a note if this used to work in an earlier version. -->

/label ~bug ~triage
