#!/bin/bash
# device-smoke.sh - the CodeBrix.Android device smoke test (AP1.7).
#
# Starts the agent emulator headless with a cold boot (`android-uireqs-avd.sh start`: AVD CodeBrix_Agent_15inch,
# created by the same script if missing, on port 5600 = serial emulator-5600; never the default port 5554;
# no Quick Boot snapshot is loaded or saved, and stale lock files are cleared when no emulator for the AVD
# runs), deploys samples/HelloPaste
# (dotnet build -t:Install; Debug uses fast deployment, never a raw `adb install`), and for each
# start page (JustBetweenUs "jbu", PdfSideBySide "pdf"):
#   - cold-starts the app, waits for the self-check summary line in logcat
#     ("SELFCHECK SUMMARY pass=N fail=M"),
#   - captures a screencap and the visual-tree dump in portrait (the device's own size),
#   - switches the display to landscape, waits for Core to re-lay out the window, captures a second
#     screencap and tree dump, and returns to portrait. --landscape rotate (the default) really rotates the
#     display (`cmd window user-rotation lock 1`; the API 37 AVD ignores `settings put system user_rotation`),
#     --landscape size swaps the display size instead (`wm size <h>x<w>`, the AP1-AP4 behaviour),
#   - stops the app.
# Then stops the emulator with `android-uireqs-avd.sh stop` - only the one this script started (unless
# --keep-emulator). An emulator that is
# already running on the port is used only when it runs the requested AVD, and is never stopped.
# Exit status: 0 when every self-check passed, both orientations re-laid out and nothing crashed;
# 1 otherwise; 2 for a setup failure (no emulator, build/deploy failed).
#
# usage: build/test-scripts/device-smoke.sh [-c Debug|Release] [--avd NAME] [--port PORT]
#                                           [--pages "jbu pdf"] [--out DIR] [--keep-emulator]
#                                           [--landscape rotate|size]
# env:   CODEBRIX_ANDROID_BUILD_LOCK  a lock file; when set, the build/deploy and the emulator start/stop
#                                     run under `flock -o <lock>`
#        ANDROID_HOME                 the Android SDK (default ~/Android/Sdk)
set -u
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
config=Debug
avd=CodeBrix_Agent_15inch
port=5600
pages="jbu pdf"
out=""
keep=0
landscape=rotate
while [ $# -gt 0 ]; do
  case "$1" in
    -c) config=$2; shift 2 ;;
    --avd) avd=$2; shift 2 ;;
    --port) port=$2; shift 2 ;;
    --pages) pages=$2; shift 2 ;;
    --out) out=$2; shift 2 ;;
    --keep-emulator) keep=1; shift ;;
    --landscape) landscape=$2; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done
if [ "$port" = 5554 ]; then echo "refusing port 5554 (the default emulator port may be Jeremy's)" >&2; exit 2; fi
serial=emulator-$port
sdk=${ANDROID_HOME:-$HOME/Android/Sdk}
adb="$sdk/platform-tools/adb"
avdsh="$here/android-uireqs-avd.sh"
lock=${CODEBRIX_ANDROID_BUILD_LOCK:-}
out=${out:-$repo/artifacts/device-smoke/$(date +%Y%m%d-%H%M%S)-$config}
mkdir -p "$out"
package=com.codebrix.hellopaste
activity=$package/com.codebrix.hellopaste.MainActivity
app="$repo/samples/HelloPaste/HelloPaste.Android/HelloPaste.Android.csproj"
export MSBUILDDISABLENODEREUSE=1

log() { echo "[$(date +%H:%M:%S)] $*"; }
locked() { if [ -n "$lock" ]; then flock -o "$lock" "$@"; else "$@"; fi; }
online() { [ "$("$adb" -s "$serial" get-state 2>/dev/null)" = device ]; }

# 1. Emulator. The script only ever uses (and stops) the AVD it was asked for.
avd_name() { "$adb" -s "$serial" emu avd name 2>/dev/null | head -1 | tr -d '\r'; }
started=0
if ! online; then
  log "starting emulator $avd (headless, cold boot)"
  if ! locked "$avdsh" start --name "$avd" --port "$port" --cold --log "$out/emulator.log"; then
    log "FAIL: the emulator did not start (see $out/emulator.log)"; exit 2
  fi
  started=1
fi
if online && [ "$(avd_name)" != "$avd" ]; then
  log "FAIL: $serial runs AVD '$(avd_name)', not '$avd' - it is not this script's device; nothing was deployed"
  exit 2
fi
for _ in $(seq 1 90); do [ "$("$adb" -s "$serial" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ] && break; sleep 2; done
if [ "$("$adb" -s "$serial" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" != 1 ]; then
  log "FAIL: device $serial did not boot"; exit 2
fi
# A freshly booted emulator can drop offline for a moment; wait until the package manager answers.
for _ in $(seq 1 60); do online && "$adb" -s "$serial" shell pm path android > /dev/null 2>&1 && break; sleep 2; done
size=$("$adb" -s "$serial" shell wm size | sed -n 's/Physical size: //p' | tr -d '\r')
w=${size%x*}; h=${size#*x}
log "device $serial online, physical size ${w}x${h}"

stop_emulator() {
  if [ $started -eq 1 ] && [ $keep -eq 0 ]; then
    # The AVD script stops the emulator on the port only when it runs $avd, and waits for its process to exit.
    locked "$avdsh" stop --name "$avd" --port "$port"
  fi
}

# 2. Build and deploy.
log "building and deploying HelloPaste ($config)"
locked dotnet build "$app" -c "$config" -t:Install "-p:AdbTarget=-s $serial" -nologo > "$out/build.log" 2>&1
rc=$?
if [ $rc -ne 0 ] && grep -q -E 'device offline|device not found|AdbException' "$out/build.log"; then
  log "deploy hit an adb connection error; waiting for the device and retrying once"
  "$adb" -s "$serial" wait-for-device
  for _ in $(seq 1 60); do online && "$adb" -s "$serial" shell pm path android > /dev/null 2>&1 && break; sleep 2; done
  locked dotnet build "$app" -c "$config" -t:Install "-p:AdbTarget=-s $serial" -nologo > "$out/build.log" 2>&1
  rc=$?
fi
grep -E '^ +[0-9]+ (Warning|Error)\(s\)|Time Elapsed' "$out/build.log" | sed 's/^ */  /'
if [ $rc -ne 0 ]; then log "FAIL: build/deploy rc=$rc (see $out/build.log)"; stop_emulator; exit 2; fi

# Waits until a pattern appears in a file (seconds); returns 1 on timeout.
wait_for() { local file=$1 pattern=$2 secs=$3; for _ in $(seq 1 $((secs * 2))); do grep -q -E "$pattern" "$file" 2>/dev/null && return 0; sleep 0.5; done; return 1; }

# Writes the last visual-tree dump of a logcat file.
last_tree() { awk '/Visual tree after layout/{buf=""} /CodeBrix.Android.UI.VisualTree:/{sub(/^.*CodeBrix.Android.UI.VisualTree: /,""); buf=buf $0 "\n"} END{printf "%s", buf}' "$1"; }

failed=0
for page in $pages; do
  tag="HelloPaste_${config}_${page}"
  logcat="$out/$tag.logcat.txt"
  log "page $page: cold start"
  "$adb" -s "$serial" shell wm size reset
  "$adb" -s "$serial" shell settings put system accelerometer_rotation 0
  "$adb" -s "$serial" shell cmd window user-rotation lock 0
  "$adb" -s "$serial" shell am force-stop $package
  "$adb" -s "$serial" logcat -c
  "$adb" -s "$serial" logcat -v brief > "$logcat" 2>&1 &
  logcat_pid=$!
  "$adb" -s "$serial" shell am start -W -n $activity --es page "$page" | grep -E 'Status|LaunchState|TotalTime' | sed 's/^/  /'

  if wait_for "$logcat" 'SELFCHECK SUMMARY' 120; then
    summary=$(grep -E 'SELFCHECK SUMMARY' "$logcat" | tail -1 | sed 's/^.*SELFCHECK SUMMARY/SUMMARY/')
    log "page $page: $summary"
    grep -E 'SELFCHECK .* FAIL ' "$logcat" | sed 's/^.*SELFCHECK /  FAIL: /'
    echo "$summary" | grep -q 'fail=0' || failed=1
  else
    log "page $page: FAIL - no SELFCHECK SUMMARY within 120 s"; failed=1
  fi
  sleep 1
  "$adb" -s "$serial" exec-out screencap -p > "$out/${tag}_portrait.png"
  last_tree "$logcat" > "$out/${tag}_portrait_tree.txt"
  log "page $page: portrait ${w}x${h} captured ($(wc -l < "$out/${tag}_portrait_tree.txt") tree lines)"

  # Landscape: rotate the display (or swap its size); Core must re-lay out the window without re-creating
  # the activity.
  marks=$(grep -c 'Visual tree after layout' "$logcat")
  if [ "$landscape" = size ]; then
    "$adb" -s "$serial" shell wm size "${h}x${w}"
  else
    "$adb" -s "$serial" shell cmd window user-rotation lock 1
  fi
  if wait_for "$logcat" "XamlIslandRoot slot=0,0,${h}x${w}" 30; then
    sleep 2
    "$adb" -s "$serial" exec-out screencap -p > "$out/${tag}_landscape.png"
    last_tree "$logcat" > "$out/${tag}_landscape_tree.txt"
    log "page $page: landscape ${h}x${w} captured ($(wc -l < "$out/${tag}_landscape_tree.txt") tree lines, $(( $(grep -c 'Visual tree after layout' "$logcat") - marks )) re-layouts)"
  else
    log "page $page: FAIL - no re-layout to ${h}x${w} within 30 s"; failed=1
  fi
  "$adb" -s "$serial" shell wm size reset
  "$adb" -s "$serial" shell cmd window user-rotation lock 0

  if grep -q -E 'FATAL EXCEPTION|AndroidRuntime.*(Shutting down|FATAL)' "$logcat"; then
    log "page $page: FAIL - the app crashed"; grep -E -A5 'FATAL EXCEPTION' "$logcat" | head -20; failed=1
  fi
  pid=$("$adb" -s "$serial" shell pidof $package | tr -d '\r')
  [ -z "$pid" ] && { log "page $page: FAIL - the app is not running at the end"; failed=1; }

  "$adb" -s "$serial" shell am force-stop $package
  kill $logcat_pid 2>/dev/null; wait $logcat_pid 2>/dev/null
done

stop_emulator
log "device smoke: $([ $failed -eq 0 ] && echo PASS || echo FAIL) (output: $out)"
exit $failed
