# Map Secrets

A separate BepInEx mod for Castlevania: Belmont's Curse. Adds configurable collectible, activity and hidden-path markers to the area map, using artwork and the serif font from your installed game.

Version 0.1.3 audits 384 playable room scenes across all 10 world variants, including sub-areas. The catalog has 672 marker placements. The alternate ending area was audited and has no relevant collectible placements. See the coverage and completion limits below.

## Screenshots

![Grim Token marker hover label](https://raw.githubusercontent.com/ollieatkinson/castlevania-belmonts-curse-map-secrets/trunk/docs/screenshots/marker-tooltip.png)

[View the area map and filter-menu gallery](https://github.com/ollieatkinson/castlevania-belmonts-curse-map-secrets/tree/trunk/docs/screenshots).

## Install

Requires BepInEx 6.0.788, Unity IL2CPP, Windows x64. Close the game and extract the plugin ZIP into the game directory. It installs `BepInEx/plugins/MapAssist.dll`. The standalone ZIP includes BepInEx for a fresh installation. On Linux/Proton use `WINEDLLOVERRIDES="winhttp=n,b" %command%` in Steam launch options.

The DLL, plugin ID and configuration paths retain their Map Assist preview names for upgrade compatibility. Extracting this release replaces the preview DLL and preserves your settings.

Restart after replacing the DLL. The mod runs alongside Mirror Fast Travel. Remove its DLL to uninstall; your game saves are never modified.

## Map filters

Press **F5** on the area map. The native-style panel has three tabs:

- **Items:** health hearts, mana hearts, attack/defence books, optional experience sources, keepsakes, abilities, arcana, relics, weapons, clues.
- **Exploration:** challenge rooms, objectives, puzzles, lore, hidden paths, breakable walls.
- **Options:** overlay, hide collected/found, unexplored rooms, uncertain locations, first-visit bell, marker tooltips, marker size, reload configuration.

Every category has its own persistent toggle, native icon and visible count. Existing choices are retained. New categories start disabled so you can choose what to reveal without crowding the map. F6 toggles the overlay, F7 health, F8 mana and F10 hidden paths. Marker size defaults to 40 pixels at 1080p, accepts 12–80, and scales with resolution. Markers keep their screen size while zooming and follow the selected area/sub-area.

Settings are in `BepInEx/config/ollieatkinson.mapassist.cfg`. Edit it while the game runs, then click **Reload configuration** to apply changes.

Hover a visible marker with the mouse or move the game's map cursor over it to see its name and category in a small native-font label. The marker gently enlarges and brightens. Labels distinguish permanent upgrades from experience sources and identify unknown completion states. Labels stay within the screen and hide over the filter panel, during confirmations, or when markers are filtered out. Disable **Marker tooltips** in Options if preferred.

Experience sources have their own toggle, **off by default**. The 147 scene placements share a prefab save ID, so their collection state is unknown; the mod no longer presents those as permanent upgrades. **Attack / defence books** contains only the 28 permanent books.

The panel and hover labels close when leaving the area map, including teleport/loading transitions and switching map views, even when preloaded map objects stay alive.

## Room updates and bell

The mod recalculates live pickup, encounter and secret discovery states every quarter-second while playing, including entry into newly revealed rooms. The next map opening uses current inventory and exploration. Revealing a passage inside the same room also updates its linked markers.

The optional bell sounds once when you first enter an unexplored room containing remaining markers in an enabled category. Existing explored rooms, repeated chunks, revisits and respawns do not trigger another bell in that save session. It waits briefly for room initialization and checks the current room/save before sounding. Turn it off in Options or set `[Notifications] FirstVisitBell = false`. `BellVolume` controls its volume from 0 to 1; the bell uses Unity audio independently of the game's FMOD volume mixer.

## Coverage and completion limits

The game supplies 447 native placements/reward-room entries. Native pickups and chests use scene positions and save IDs; some scripted rewards and clues mark their declared room rather than an exact spawn point. Conditional NPC rewards can have alternative locations; obtaining the underlying item hides its other placements. The expanded audit includes 24 health and 24 mana hearts: the older extractor missed a mana heart with a numeric object-name suffix.

The [Gamer Guides Paris map](https://www.gamerguides.com/castlevania-belmonts-curse/maps/paris) supplies the category/reference cross-check, including all 16 puzzles, 6 objectives, 16 lore locations and 102 hidden-path reference entries. Native coordinates are calibrated against named equipment locations. Reference positions remain approximate and have not all been manually play-tested. The factual source snapshot includes marker IDs, types, names and coordinates; it contains no guide prose or images.

Seventy-three hidden-path entries link to native persistent secret-zone discovery IDs. Resettable ambush spots and silent scenery volumes are excluded. The other 29 paths do not have a confident native discovery link. Together with other uncertain room assignments, **43 locations start hidden** behind **Unverified locations**. This setting concerns mapping confidence, not reachability.

Collected inventory items, activated pickups, completed encounters and matched secret discoveries disappear when the game reports completion. Some reference challenges, puzzles, walls and objectives do not yet have a proven completion signal. They remain visible rather than being guessed complete. Passing near them is not completion. Lore close-ups observed while using the mod are remembered per save in `BepInEx/config/MapAssist/`; previously read lore without a native read flag may still appear initially. Map Secrets also remembers actual runtime pickup IDs when they differ from older scene metadata, so visiting a room can correct those marker states. Raw coverage and remaining uncertainties are in `data/coverage.json` in the source repository.

Ability-based reachability filtering is not implemented. No personal map-marker slots are used.

Scene editor offsets are excluded from map placement: marker coordinates are measured from each room’s canonical chunk origin, rather than its scene-loading container.

## Build, package and validate

Run the game with BepInEx once to generate interop assemblies, then use the .NET 8 SDK:

```sh
dotnet build -c Release -p:GamePath="/path/to/Castlevania Belmont's Curse"
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/package.sh
dotnet run --project tests/MapAssist.Tests.csproj -c Release
```

Plugin and standalone ZIPs with checksums are written to `dist/`. The standalone uses pinned BepInEx 788 with dependency licenses and corresponding sources. Game assemblies, artwork, fonts, saves and metadata caches are not packaged.

Icon asset types and sprite names can be checked against the installed game with UnityPy:

```sh
python tools/validate_icons.py "/path/to/Castlevania Belmont's Curse"
```

The filter menu opens independently of marker artwork. If a game asset cannot load, its marker uses the native eye as a fallback; that marker alone is omitted if the fallback is also unavailable. F5 restores the panel even if `ShowControls` was disabled.

The marker catalog is matched to the SHA-256 hash of `GameAssembly.dll`. Unsupported builds disable the mod and log the reason to `BepInEx/LogOutput.log`.

To regenerate the audit and coverage after a game update, install Python 3.11+ and UnityPy:

```sh
python tools/extract_markers.py "/path/to/Castlevania Belmont's Curse" data/markers.json
```

The pipeline caches local metadata in ignored `.build/catalog`, re-audits playable rooms, calibrates the attributed reference snapshot, and emits the marker catalog and coverage report. A game update also requires reviewing reference matches and completion mappings before publishing.

To publish a tested version, commit and push the reviewed changes, then run `scripts/release.sh v0.1.3` with `GamePath` set. Nothing is published automatically by building or installing.
