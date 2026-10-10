#!/usr/bin/env bash
# One-step install or update of BepInEx + Vanilla+ for Dave the Diver on Bazzite / SteamOS (Steam, Proton).
#
# Run in Desktop Mode with the game closed:
#   curl -fsSL https://raw.githubusercontent.com/OverworkedStudent/davediver/main/install-bazzite.sh | bash
#
# Run it again at any time to update the plugin. Saves are never touched.
# If the game is somewhere unusual, point at it:
#   curl -fsSL <same url> | GAME_DIR="/path/to/Dave the Diver" bash
set -euo pipefail

# Everything lives in main() so a download that is cut off half way runs nothing at all.
main() {
  local BEPINEX_URL="${VANILLAPLUS_BEPINEX_URL:-https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip}"
  local PLUGIN_URL="${VANILLAPLUS_PLUGIN_URL:-https://raw.githubusercontent.com/OverworkedStudent/davediver/main/dist/VanillaPlus.dll}"
  local GAME_DIR_NAME="Dave the Diver"
  local GAME_EXE="DaveTheDiver.exe"
  local CFG_NAME="vanillaplus.davethediver.cfg"
  # Raised only when the plugin's default settings change in a way every device should pick up.
  local SETTINGS_REV=2

  say() { printf '\n== %s\n' "$*"; }
  die() { printf '\nERROR: %s\n' "$*" >&2; exit 1; }

  [ "${EUID:-$(id -u)}" -ne 0 ] || die "Run this as your normal user, without sudo. Steam's files belong to you, not root."
  command -v curl >/dev/null 2>&1 || die "curl is not installed."

  if command -v pgrep >/dev/null 2>&1 && pgrep -f "$GAME_EXE" >/dev/null 2>&1; then
    die "Dave the Diver is running. Close it and run this again."
  fi

  # ---- find the game: every Steam library Steam knows about, then removable media as a fallback
  local steam_roots=(
    "$HOME/.local/share/Steam"
    "$HOME/.steam/steam"
    "$HOME/.steam/root"
    "$HOME/.var/app/com.valvesoftware.Steam/data/Steam"
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
  )
  local libraries=() root vdf path extra
  for root in "${steam_roots[@]}"; do
    [ -d "$root" ] || continue
    libraries+=("$root")
    for vdf in "$root/steamapps/libraryfolders.vdf" "$root/config/libraryfolders.vdf"; do
      [ -f "$vdf" ] || continue
      while IFS= read -r path; do
        if [ -n "$path" ]; then libraries+=("$path"); fi
      done < <(sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$vdf")
    done
  done
  for extra in /run/media/*/* /run/media/* /var/mnt/* /mnt/*; do
    if [ -d "$extra/steamapps" ]; then libraries+=("$extra"); fi
    if [ -d "$extra/SteamLibrary/steamapps" ]; then libraries+=("$extra/SteamLibrary"); fi
  done

  local game="${GAME_DIR:-}" lib
  if [ -n "$game" ]; then
    [ -f "$game/$GAME_EXE" ] || die "GAME_DIR is set to '$game' but $GAME_EXE is not in it."
  else
    for lib in ${libraries[@]+"${libraries[@]}"}; do
      if [ -f "$lib/steamapps/common/$GAME_DIR_NAME/$GAME_EXE" ]; then
        game="$lib/steamapps/common/$GAME_DIR_NAME"
        break
      fi
    done
  fi
  [ -n "$game" ] || die "Could not find the game. Install Dave the Diver through Steam first, or set GAME_DIR (see the top of this script)."
  [ -w "$game" ] || die "The game folder is not writable: $game"
  say "Game found: $game"

  local tmp
  tmp="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$tmp'" EXIT

  extract_zip() {
    if command -v unzip >/dev/null 2>&1; then
      unzip -oq "$1" -d "$2"
    elif command -v python3 >/dev/null 2>&1; then
      python3 -c 'import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$1" "$2"
    elif command -v bsdtar >/dev/null 2>&1; then
      bsdtar -xf "$1" -C "$2"
    else
      die "Need one of unzip, python3 or bsdtar to unpack BepInEx."
    fi
  }

  # ---- BepInEx: only installed when missing or incomplete
  if [ -f "$game/winhttp.dll" ] && [ -f "$game/doorstop_config.ini" ] && [ -f "$game/BepInEx/core/BepInEx.Unity.IL2CPP.dll" ] && [ -d "$game/dotnet" ]; then
    say "BepInEx is already installed, leaving it as it is"
  else
    say "Downloading BepInEx 6 (be.788)"
    curl -fL --progress-bar "$BEPINEX_URL" -o "$tmp/bepinex.zip" \
      || die "Could not download BepInEx. Check your internet connection and try again. Nothing was changed."
    # A zip starts with "PK"; anything else is an error page saved under the wrong name.
    [ "$(head -c 2 "$tmp/bepinex.zip")" = "PK" ] || die "The BepInEx download is not a zip file. Check your connection and try again."
    say "Extracting BepInEx into the game folder"
    extract_zip "$tmp/bepinex.zip" "$game"
    [ -f "$game/BepInEx/core/BepInEx.Unity.IL2CPP.dll" ] || die "BepInEx did not unpack correctly."
  fi

  # ---- plugin: downloaded and checked first, so a bad download never replaces a working copy
  say "Installing the Vanilla+ plugin"
  curl -fL --progress-bar "$PLUGIN_URL" -o "$tmp/VanillaPlus.dll" \
    || die "Could not download the plugin. The plugin you already had, if any, was left as it is. Check your internet connection and try again."
  local size
  size="$(wc -c < "$tmp/VanillaPlus.dll" | tr -d '[:space:]')"
  if [ "$(head -c 2 "$tmp/VanillaPlus.dll")" != "MZ" ] || [ "$size" -lt 20000 ]; then
    die "The plugin download is not a valid file ($size bytes). The plugin you already had, if any, was left as it is. Check your connection and try again."
  fi
  mkdir -p "$game/BepInEx/plugins/VanillaPlus"
  cp -f "$tmp/VanillaPlus.dll" "$game/BepInEx/plugins/VanillaPlus/VanillaPlus.dll"
  echo "   plugin installed ($size bytes)"

  # ---- settings: kept between updates; reset once when the defaults themselves have moved on
  local cfg_dir="$game/BepInEx/config" cfg rev_file have_rev backup
  cfg="$cfg_dir/$CFG_NAME"
  rev_file="$cfg_dir/.vanillaplus-settings-rev"
  mkdir -p "$cfg_dir"
  have_rev="$(cat "$rev_file" 2>/dev/null || true)"
  if [ -f "$cfg" ] && [ "$have_rev" != "$SETTINGS_REV" ]; then
    backup="$cfg.$(date +%Y%m%d-%H%M%S).bak"
    mv -f "$cfg" "$backup"
    say "Settings reset to this build's defaults (your old file is kept as $(basename "$backup"))"
  elif [ -f "$cfg" ]; then
    say "Your settings were kept as they are"
  fi
  printf '%s\n' "$SETTINGS_REV" > "$rev_file"

  # ---- launch option: cannot be set from here, but Steam's own settings file says whether it is there.
  # Only Dave the Diver's own block (app 1868140) is read, so another game's launch option does not count.
  local launch_set=no cfgv
  if command -v awk >/dev/null 2>&1; then
    for root in "${steam_roots[@]}"; do
      for cfgv in "$root"/userdata/*/config/localconfig.vdf; do
        [ -f "$cfgv" ] || continue
        if awk '
            /^[[:space:]]*"1868140"[[:space:]]*$/ { inapp = 1; depth = 0; next }
            inapp && /^[[:space:]]*\{/ { depth++; next }
            inapp && /^[[:space:]]*\}/ { depth--; if (depth <= 0) inapp = 0; next }
            inapp && /"LaunchOptions"/ && /winhttp=n,b/ { found = 1 }
            END { exit found ? 0 : 1 }
          ' "$cfgv" 2>/dev/null; then
          launch_set=yes
        fi
      done
    done
  fi

  if [ "$launch_set" = yes ]; then
    cat <<'EOF'

== Done. The launch option is already set for Dave the Diver in Steam, so just
   start the game. For reference, it is this, under
   Dave the Diver -> Properties -> General -> Launch Options:

      WINEDLLOVERRIDES="winhttp=n,b" %command%
EOF
  else
    cat <<'EOF'

== Done. One thing left that only you can do, once (skip it if you have
   already done it; Steam can take a while to save it where this can see it):

  In Steam: Dave the Diver -> Properties -> General -> Launch Options, paste:

      WINEDLLOVERRIDES="winhttp=n,b" %command%

  Without it Proton ignores BepInEx and nothing loads.
EOF
  fi

  cat <<'EOF'

  The first start after installing BepInEx takes a few minutes with a black
  screen or a console window while it prepares itself. That is normal.

  Map toggle on a controller: click both sticks in together (L3 + R3).
  To check it loaded: the game folder gets a BepInEx/VanillaPlus-logs folder
  with one file per session.
EOF
}

main "$@"
