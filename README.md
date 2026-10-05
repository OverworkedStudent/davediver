# Dave the Diver: Vanilla+

A small quality-of-life plugin for Dave the Diver (Steam) built on BepInEx 6 IL2CPP. It keeps the vanilla feel: every feature has its own on/off toggle and the multipliers are modest. It adds no HUD elements, no fish health bars, no auto-pickup and no item spawning, and it never touches save data.

## Status

| Feature | State |
| --- | --- |
| Plugin skeleton, config, startup logging | Done |
| Clarity toggle (remove edge blur and darkening) | Implemented, awaiting in-game test |
| FPS unlock | Implemented, awaiting in-game test. See the FPS warning below |
| Easier harpoon struggle | Implemented, awaiting in-game test |
| Perfect pour tip buff | Not implemented yet. A log-only probe gathers the data first |

Nothing here has been confirmed in game yet.

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
4. Copy `VanillaPlus.dll` to `BepInEx\plugins\VanillaPlus\`.
5. Launch the game. The config file appears at `BepInEx\config\vanillaplus.davethediver.cfg`.

## Install: Bazzite / Steam Deck style Linux (Proton)

The game runs through Proton, so use the same **win-x64** BepInEx build and the same `VanillaPlus.dll`.

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
| HarpoonStruggle | StruggleMultiplier | 1.5 | Progress multiplier per input. 1.0 = vanilla. Range 1 to 3. |
| PerfectPourTip | Enabled | true | Slightly raise tip chance after a perfect green tea or beer pour. |
| PerfectPourTip | PerfectPourTipMultiplier | 1.15 | Tip chance multiplier, clamped to the game's maximum. 1.0 = vanilla. Range 1 to 2. |
| Debug | TipProbe | true | Temporary. Logs sushi bar drink, payment and tip chance calls. Changes nothing in the game. |

### FPS warning

The game is built around 60 FPS. The existing Nexus mod "FPS Unlocker - In the Jungle Compatible" ships an optional "movement compensation above 60 FPS" patch, which indicates that at least player movement is tied to frame rate. This plugin does not compensate for that. With `TargetFPS` above 60, expect movement and possibly some mini-games or timers to run faster. Leave it at 0 if anything feels off.

## Checking that it loaded

Open `BepInEx/LogOutput.log` and look for lines from the `VanillaPlus` source:

```
[Info   :VanillaPlus] VanillaPlus 0.1.0 loading
[Info   :VanillaPlus] Game version: ... (Unity 6000.0.52f1)
[Info   :VanillaPlus] Feature Clarity: ON
[Info   :VanillaPlus] Feature FPS unlock: OFF (TargetFPS=0)
[Info   :VanillaPlus] Feature Harpoon struggle: ON (StruggleMultiplier=1.5)
[Info   :VanillaPlus] Feature Perfect pour tip: ON (PerfectPourTipMultiplier=1.15)
```

## Uninstall

- Plugin only: delete `BepInEx/plugins/VanillaPlus/` and `BepInEx/config/vanillaplus.davethediver.cfg`.
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

Project setup and class discovery were informed by [WhiteMinds/dave-diver-expansion](https://github.com/WhiteMinds/dave-diver-expansion) (MIT) and [devopsdinosaur/dave-the-diver-mods](https://github.com/devopsdinosaur/dave-the-diver-mods). No code is copied from either.
