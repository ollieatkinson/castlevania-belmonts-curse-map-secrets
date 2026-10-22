# Map Secrets

Adds collectible and secret passage markers to the map in Castlevania: Belmont's Curse. Choose which categories to show, hover markers for their names, and hide items you've already collected. Uses the game's icons and menu font.

![Marker tooltip](https://raw.githubusercontent.com/ollieatkinson/castlevania-belmonts-curse-map-secrets/trunk/docs/screenshots/marker-tooltip.png)

[More screenshots](docs/screenshots)

## Installation

Close the game and extract the **whole ZIP into the main game folder**, beside `Castlevania Belmont's Curse.exe`. **Do not extract into `BepInEx` or `plugins`.** Choose one ZIP:

- **Plugin:** for an existing BepInEx 6.0.788 installation.
- **Standalone:** includes BepInEx for a fresh installation.

On Windows, you can extract the standalone anywhere and run `Install.cmd` to find the game folder or select the game EXE. It handles a ZIP accidentally extracted inside `BepInEx`.

The correct mod path is `BepInEx/plugins/MapAssist.dll`, with only one `BepInEx`. The loose `winhttp.dll` belongs beside the game EXE. See `INSTALL.txt` for the folder layout. Allow a few minutes for the first launch; if nothing loads, share `BepInEx/LogOutput.log`.

On Linux/Proton, set Steam launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

Updates replace `BepInEx/plugins/MapAssist.dll` and keep your settings. Remove that DLL to uninstall.

## Controls and settings

Open the area map and press **F4** for the filters. Each category has its own toggle. Options include hiding collected items, unexplored rooms, marker size, hover labels and a bell when entering a new room with items remaining.

Change shortcuts in `BepInEx/config/ollieatkinson.mapassist.cfg`:

```ini
[Hotkeys]
Menu = F4
Overlay = F6
Health = F7
Mana = F8
Secrets = F10
```

Use key names such as `F4`, `Insert` or `M`. Set a shortcut to `None` to disable it. Restart the game after editing, or click **Reload configuration** in the map menu. F4 replaces the old F5 default to avoid quick-save conflicts.

Experience statues have a separate toggle, off by default. Collected statues are hidden when **Hide collected / found** is enabled, including those collected before installing the mod.

Native breakable walls also use their individual save IDs, so previously broken walls can be hidden without revisiting them.

Spirit ghosts that grant clues appear under **Clues**, at their interaction points. Clue markers hide after obtaining the corresponding clue when **Hide collected / found** is enabled.

## Known limits

Some activities and hidden paths have no confirmed completion flag and may remain visible after finishing them, including on a 100% save. Their tooltip says **Completion not tracked**; **Hide collected / found** keeps them visible. **Completion unknown** means a marker has completion tracking but its current state could not be determined. Uncertain locations are hidden unless you enable **Unverified locations**. Previously read lore may appear until read again with the mod installed; tracked lore says **Read to mark as found**. Markers aren't filtered by your unlocked abilities.

The mod doesn't change your game saves. The current marker catalog was checked against Steam build **25975867**, but other builds, including Xbox app on PC, can load without matching its DLL hash. This does not guarantee compatibility with every build. Startup logs the game's version and `GameAssembly.dll` SHA-256 in `BepInEx/LogOutput.log`; a different or unreadable hash does not block loading. Hooks and components load independently, and features fall back only when they fail. If collection or exploration filtering fails, markers remain visible and filtering is retried the next time you open the map. Include the log when reporting compatibility problems.

## Building

Requires the .NET 8 SDK and a game installation that has run BepInEx once.

```sh
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/package.sh
```

Plugin and standalone ZIPs are written to `dist/`. To rebuild the marker data after a game update, use `tools/extract_markers.py` with Python and UnityPy.

## Thanks

The BepInEx team, and [Gamer Guides](https://www.gamerguides.com/castlevania-belmonts-curse/maps/paris) for the map used to cross-check locations.
