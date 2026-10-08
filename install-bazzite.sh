#!/usr/bin/env bash
# One-step install of BepInEx + Vanilla+ for Dave the Diver on Bazzite / SteamOS (Steam, Proton).
# Run in Desktop Mode with the game closed:
#   curl -fsSL https://raw.githubusercontent.com/OverworkedStudent/davediver/main/install-bazzite.sh | bash
# Running it again updates the plugin. Saves are not touched.
set -euo pipefail

BEPINEX_URL="https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip"
PLUGIN_URL="https://raw.githubusercontent.com/OverworkedStudent/davediver/main/dist/VanillaPlus.dll"
GAME_DIR_NAME="Dave the Diver"

say() { printf '\n== %s\n' "$*"; }

if pgrep -f "DaveTheDiver.exe" >/dev/null 2>&1; then
  echo "Dave the Diver is running. Close it and run this again."; exit 1
fi

# Look in every Steam library: internal drive, Flatpak Steam, and SD cards / extra drives.
game=""
while IFS= read -r candidate; do
  if [ -f "$candidate/DaveTheDiver.exe" ]; then game="$candidate"; break; fi
done < <(
  for root in "$HOME/.local/share/Steam" "$HOME/.steam/steam" "$HOME/.var/app/com.valvesoftware.Steam/data/Steam" /run/media/*/* /run/media/* /var/mnt/* /mnt/*; do
    [ -d "$root" ] || continue
    printf '%s\n' "$root/steamapps/common/$GAME_DIR_NAME" "$root/SteamLibrary/steamapps/common/$GAME_DIR_NAME"
  done
)

if [ -z "$game" ]; then
  echo "Could not find the game. Install Dave the Diver through Steam first, then run this again."; exit 1
fi
say "Game found: $game"

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT

if [ -f "$game/winhttp.dll" ] && [ -d "$game/BepInEx/core" ]; then
  say "BepInEx already installed, leaving it as it is"
else
  say "Downloading BepInEx 6 (be.788)"
  curl -fL --progress-bar "$BEPINEX_URL" -o "$tmp/bepinex.zip"
  say "Extracting BepInEx into the game folder"
  python3 - "$tmp/bepinex.zip" "$game" <<'PY'
import sys, zipfile
zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])
PY
fi

say "Installing the Vanilla+ plugin"
mkdir -p "$game/BepInEx/plugins/VanillaPlus"
curl -fL --progress-bar "$PLUGIN_URL" -o "$tmp/VanillaPlus.dll"
cp -f "$tmp/VanillaPlus.dll" "$game/BepInEx/plugins/VanillaPlus/VanillaPlus.dll"

cat <<'EOF'

== Done. One thing left that only you can do, once:

  In Steam: Dave the Diver -> Properties -> General -> Launch Options, paste:

      WINEDLLOVERRIDES="winhttp=n,b" %command%

  Then start the game. The first start takes a few minutes with a black
  screen or console while BepInEx prepares itself. That is normal.

  Map toggle on a controller: click both sticks in together (L3 + R3).
EOF
