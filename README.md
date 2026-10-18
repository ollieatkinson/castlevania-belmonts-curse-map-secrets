# Map Secrets

Adds collectible and secret passage markers to the map in Castlevania: Belmont's Curse. Choose which categories to show, hover markers for their names, and hide items you've already collected. Uses the game's icons and menu font.

![Marker tooltip](https://raw.githubusercontent.com/ollieatkinson/castlevania-belmonts-curse-map-secrets/trunk/docs/screenshots/marker-tooltip.png)

[More screenshots](docs/screenshots)

## Installation

Close the game and extract one ZIP into the game folder:

- **Plugin:** for an existing BepInEx 6.0.788 installation.
- **Standalone:** includes BepInEx for a fresh installation.

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

## Known limits

Some activities and hidden paths have no confirmed completion flag and may remain visible after finishing them. Uncertain locations are hidden unless you enable **Unverified locations**. Previously read lore may appear until revisited with the mod installed. Markers aren't filtered by your unlocked abilities.

The mod doesn't change your game saves. If a game update isn't supported, check `BepInEx/LogOutput.log`.

## Building

Requires the .NET 8 SDK and a game installation that has run BepInEx once.

```sh
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/package.sh
```

Plugin and standalone ZIPs are written to `dist/`. To rebuild the marker data after a game update, use `tools/extract_markers.py` with Python and UnityPy.

## Thanks

The BepInEx team, and [Gamer Guides](https://www.gamerguides.com/castlevania-belmonts-curse/maps/paris) for the map used to cross-check locations.
