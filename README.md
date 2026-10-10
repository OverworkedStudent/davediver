# Dave the Diver: Vanilla+

A quality-of-life plugin for Dave the Diver (Steam) built on BepInEx 6 IL2CPP. It keeps the vanilla feel: every feature has its own on/off toggle and its strength is a setting. It adds no fish health bars and no item spawning, and it never touches save data. The one HUD addition is an optional dive map that stays off until you call it up.

## Status

| Feature | State |
| --- | --- |
| Plugin skeleton, config, startup logging | Done |
| Clarity toggle (remove edge blur and darkening) | Working in game |
| FPS unlock | Implemented, never tested in game. Off by default. See the FPS warning below |
| Easier harpoon struggle | Working in game |
| Crab trap timer | Working in game |
| Auto pickup | Working in game |
| Perfect pour tip buff | Working in game |
| Dive map | Working in game |
| Weapon breakdown hold time | Working in game (3.5 s down to 1.75 s) |
| Gentler stealth (glacier passage) | Implemented, never seen to take effect in game |
| Sea People Village walk speed | Working in game |
| Fish spawn odds (tuna, marlin) | Working in game |

"Working in game" means confirmed from in-game logs on game version v1.0.6.2113.

Developed against game build 25315876 (Unity 6000.0.52f1) and BepInEx 6.0.0-be.788.

## Back up your saves first

The plugin does not read or write saves, but back them up before modding anyway.

- Windows: `%USERPROFILE%\AppData\LocalLow\nexon\DAVE THE DIVER\SteamSData`
- Bazzite / Proton: `~/.local/share/Steam/steamapps/compatdata/1868140/pfx/drive_c/users/steamuser/AppData/LocalLow/nexon/DAVE THE DIVER/SteamSData`

Copy the whole `SteamSData` folder somewhere safe.

## Install: Windows

1. Download **BepInEx 6 Bleeding Edge, Unity.IL2CPP-win-x64** from <https://builds.bepinex.dev/projects/bepinex_be>.
2. Extract the zip into the game folder so `winhttp.dll` sits next to `DaveTheDiver.exe`.
3. Launch the game once and quit at the title screen. The first launch takes a few minutes while BepInEx generates `BepInEx\interop`.
4. Copy `VanillaPlus.dll` (a prebuilt copy is in `dist/`) to `BepInEx\plugins\VanillaPlus\`.
5. Launch the game. The config file appears at `BepInEx\config\vanillaplus.davethediver.cfg`.

## Install: Bazzite / Steam Deck style Linux (Proton)

The game runs through Proton, so it uses the same **win-x64** BepInEx build and the same `VanillaPlus.dll` as Windows.

### One command

In Desktop Mode, with the game closed, open a terminal (Konsole) and run:

```
curl -fsSL https://raw.githubusercontent.com/OverworkedStudent/davediver/main/install-bazzite.sh | bash
```

It finds the game in any Steam library (internal drive or SD card), installs BepInEx if it is missing or incomplete, and installs the latest `VanillaPlus.dll`. Run it again at any time to update.

Then do step 2 below once (the launch option), and start the game. The first start takes a few minutes while BepInEx generates its files. The script tells you at the end whether it could already see the launch option in Steam.

What the script does and does not do:

- Saves are never touched.
- Your settings are kept between updates. They are reset only when a new build changes its defaults in a way worth picking up, and then the old file stays next to the new one as `vanillaplus.davethediver.cfg.<date>.bak`. The first run of this version of the script on a device set up by an older one resets them once.
- Each download is checked before anything is replaced, so a failed download leaves the plugin you already had in place.
- It refuses to run while the game is open, or with `sudo`.

If the game is not found (an unusual library location), point at the folder that holds `DaveTheDiver.exe`:

```
curl -fsSL https://raw.githubusercontent.com/OverworkedStudent/davediver/main/install-bazzite.sh | GAME_DIR="/path/to/Dave the Diver" bash
```

Where things are on Bazzite, inside the game folder (usually `~/.local/share/Steam/steamapps/common/Dave the Diver`):

- Settings: `BepInEx/config/vanillaplus.davethediver.cfg`
- This plugin's log, one file per session: `BepInEx/VanillaPlus-logs/`
- Everything BepInEx logged on the last start: `BepInEx/LogOutput.log`

If the mod does not seem to load:

- No `BepInEx/LogOutput.log` at all means BepInEx never started: the launch option is missing or mistyped.
- A log without any `VanillaPlus` lines means the plugin is not in `BepInEx/plugins/VanillaPlus/`: run the script again.
- If a black console window sits in front of the game in Gaming Mode, open `BepInEx/config/BepInEx.cfg`, find `[Logging.Console]` and set `Enabled = false`.

The script has been tested against a copy of a Steam library layout (fresh install, update, bad and cut-off downloads), not yet on every Bazzite setup. If it stops with an error, the message says what it was looking for.

### By hand

1. In Desktop Mode, extract the BepInEx zip into the game folder (Steam: right-click the game, Manage, Browse local files).
2. In Steam, open the game's Properties and set Launch Options to:

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

   Without this, Proton ignores BepInEx's `winhttp.dll` and nothing loads.
3. Launch once and wait for the interop generation to finish, then quit.
4. Copy `VanillaPlus.dll` to `BepInEx/plugins/VanillaPlus/` and launch again.

## Config

`BepInEx/config/vanillaplus.davethediver.cfg`, created on first launch. Changes apply on the next launch.

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| Clarity | Enabled | true | Master toggle: remove the blur and darkening at the screen edges. |
| Clarity | DisableVignette | true | Darkened screen edges. |
| Clarity | DisableVerticalBlur | true | The game's own blur bands at the top and bottom of the screen. |
| Clarity | DisableDepthOfField | true | Depth of field blur. |
| Clarity | DisableChromaticAberration | true | Colour fringing towards the screen edges. |
| Clarity | DisableLensDistortion | false | Lens warping. |
| FPS | Enabled | true | Allow the mod to change the frame cap. |
| FPS | TargetFPS | 0 | Frame cap. 0 = vanilla, frame rate settings untouched. Any other value also turns VSync off. Range 0 to 360. |
| HarpoonStruggle | Enabled | true | Multiply the progress each stick rock / button mash gives. Never auto-completes. |
| HarpoonStruggle | StruggleMultiplier | 3 | Progress multiplier per input. 1.0 = vanilla. Range 1 to 6. |
| WeaponBreak | Enabled | true | Shorten the hold needed to break down a weapon picked up during a dive. |
| WeaponBreak | HoldTimeMultiplier | 0.5 | Hold time multiplier. 0.5 = twice as fast. Range 0.1 to 1. |
| PerfectPourTip | Enabled | true | Slightly raise tip chance after a perfect green tea or beer pour. |
| PerfectPourTip | PerfectPourTipMultiplier | 1.15 | Tip chance multiplier, clamped to the game's maximum. 1.0 = vanilla. Range 1 to 2. |
| AutoPickup | Enabled | true | Master toggle: pick up nearby things while diving, as if the interact button had been pressed. |
| AutoPickup | AutoPickupItems | true | Dropped items and materials. Weapons and harpoon heads are never auto-picked. |
| AutoPickup | AutoPickupAmmoBox | true | Ammo boxes, unless the current gun is full. |
| AutoPickup | AutoPickupFish | false | Dead or sleeping fish that only need the interact button. |
| AutoPickup | AutoOpenChests | false | Open chests automatically. |
| AutoPickup | AutoPickupOxygenBox | true | Include oxygen chests when AutoOpenChests is on (fixed radius 1.0). |
| AutoPickup | PickupRadius | 1.0 | Pickup distance in game units. Range 0.5 to 5. |
| DiveMap | Enabled | true | Master toggle for the dive map. |
| DiveMap | StartMode | Off | How the map starts each session: Off, Mini or Big. |
| DiveMap | ToggleKey | M | Keyboard key that cycles Off, Mini, Big. |
| DiveMap | ControllerToggle | BothStickClicks | Controller shortcut: BothStickClicks (L3 and R3 together), HoldRightStickClick, SelectPlusRightStickClick, or None. |
| DiveMap | ShowFish | false | Fish as dots: yellow while the species still lacks a 3-star catch, faint once it has one. |
| DiveMap | MiniCorner | TopRight | Screen corner for the round map. TopRight is the corner the dive HUD leaves free. |
| DiveMap | MiniSize | 0.2 | Diameter of the round map as a share of screen height. Range 0.12 to 0.4. |
| DiveMap | MiniRadius | 15 | How far the round map sees around Dave, in game units. Range 8 to 40. |
| DiveMap | Opacity | 1.0 | Opacity of the map picture. Range 0.4 to 1. |
| FishSpawn | Enabled | true | Raise the odds of chosen fish at spawn points that can already produce them, and log the real odds. |
| FishSpawn | BoostedFish | Tuna,Marlin | Name fragments of the fish to favour. |
| FishSpawn | Multiplier | 3 | How much heavier those fish weigh in each spawn draw. Range 1 to 100. |
| CrabTrap | Enabled | true | Shorten how long a placed crab trap takes. |
| CrabTrap | Seconds | 5 | Longest a crab trap takes, in seconds. Range 1 to 600. |
| Stealth | Enabled | true | Make the patrolling creatures in the glacier passage slower to spot Dave. They can still catch him. |
| Stealth | DetectionSpeedMultiplier | 0.7 | How fast their alert gauge fills. 1.0 = vanilla. Range 0.25 to 1. |
| VillageSpeed | Enabled | true | Move faster in the Sea People Village only. |
| VillageSpeed | SpeedMultiplier | 1.5 | Move speed multiplier in the village. 1.0 = vanilla. Range 1 to 6. |

### Dive map

Press `M`, or click both sticks together on a controller, to cycle the map: off, a round corner map, the full level, off. It marks Dave and the ways back to the boat (escape pods and mirrors) and nothing else: no fish, chests or loot. On the corner map, when no exit is in range, a small boat icon on the rim points toward the nearest one.

The corner map is drawn as a round instrument to match the game's oxygen dial, and Dave and the exits use the game's own icons, looked up from what the game already has loaded. While the map is off it renders nothing. While it is on, the level is drawn a second time into a small texture every third frame, which costs a few frames per second on slower hardware. The map hides itself in the pause menu and cutscenes, and does not appear in the Sea People Village, which has its own map.

### FPS warning

The game is built around 60 FPS. The existing Nexus mod "FPS Unlocker - In the Jungle Compatible" ships an optional "movement compensation above 60 FPS" patch, which indicates that at least player movement is tied to frame rate. This plugin does not compensate for that. With `TargetFPS` above 60, expect movement and possibly some mini-games or timers to run faster. Leave it at 0 if anything feels off.

## Checking that it loaded

Open `BepInEx/LogOutput.log` and look for lines from the `VanillaPlus` source:

```
[Info   :VanillaPlus] VanillaPlus 1.0.1 loading
[Info   :VanillaPlus] Game version: ... (Unity 6000.0.52f1)
[Info   :VanillaPlus] Feature Clarity: ON
[Info   :VanillaPlus] Feature FPS unlock: OFF (TargetFPS=0)
[Info   :VanillaPlus] Feature Harpoon struggle: ON (StruggleMultiplier=3)
[Info   :VanillaPlus] Feature Perfect pour tip: ON (PerfectPourTipMultiplier=1.15)
```

## Uninstall

- Plugin only: delete `BepInEx/plugins/VanillaPlus/`, `BepInEx/config/vanillaplus.davethediver.cfg` (and any `.bak` copies and `.vanillaplus-settings-rev` next to it) and the `BepInEx/VanillaPlus-logs/` folder.
- Everything: also delete the `BepInEx` folder, `dotnet` folder, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` from the game folder. On Bazzite, clear the launch option too.

Saves are not affected either way.

## Building

Requires the .NET SDK (6 or newer) and a game install with BepInEx already run once.

1. Copy `GamePath.user.props.example` to `GamePath.user.props` and set `GamePath` to your install. The file is gitignored.
2. Build:

   ```
   dotnet build src/VanillaPlus -c Release
   ```

   Output: `src/VanillaPlus/bin/Release/VanillaPlus.dll`. Set `DeployToGame` to `true` in `GamePath.user.props` to copy it into the game's plugins folder on every build.

The project references the interop assemblies in place from `<GamePath>\BepInEx\interop`. No game files or generated interop assemblies belong in this repository, and `.gitignore` blocks them.

## Credits

Project setup and class discovery were informed by [WhiteMinds/dave-diver-expansion](https://github.com/WhiteMinds/dave-diver-expansion) (MIT) and [devopsdinosaur/dave-the-diver-mods](https://github.com/devopsdinosaur/dave-the-diver-mods). The auto pickup feature and the way the dive map picture is produced are adapted from dave-diver-expansion's AutoPickup and DiveMap under its MIT licence; the village speed feature uses a technique its notes document. No code is taken from dave-the-diver-mods.
