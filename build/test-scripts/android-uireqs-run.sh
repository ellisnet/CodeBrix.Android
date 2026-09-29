#!/bin/bash
# android-uireqs-run.sh - runs the UIReqs suite (the CodeBrix.Platform UIReqs scenarios, ported) on the
# agent AVD: the device app tests/CodeBrix.Android.UIReqs.Device executes the copied steps in the app,
# the host runner tests/CodeBrix.Android.UIReqs (Reqnroll + xunit.v3, Microsoft.Testing.Platform) drives
# it step by step over `adb forward` and takes the screencaps.
#
#   1. takes the Android build lock (CODEBRIX_ANDROID_BUILD_LOCK, default ~/ClaudeHome/android-buildout-work/build.lock)
#      for the WHOLE run (one build / one emulator at a time),
#   2. starts the AVD (build/test-scripts/android-uireqs-avd.sh start: CodeBrix_Agent_15inch, port 5600, cold boot),
#   3. fixes the global settings (animation scales 0, font scale 1.0, night mode off, show_touches off,
#      stay awake, no auto-rotation, default size and density); the groups in $animator_on_groups run with the
#      animator duration scale at 1 instead (set before the app starts, restored to 0 after the group),
#   4. installs the device app (Debug: dotnet build -t:Install) and builds the host runner,
#   5. for each orientation: rotates the emulator (`cmd window user-rotation lock <r>` - the API 37 AVD ignores
#      `settings put system user_rotation` alone), waits until the display really is 1080x1920 / 1920x1080
#      (a screencap's size; a rotation that does not apply is a setup failure, never a silent portrait run),
#      restarts the app, forwards the port, and runs the host runner for the group(s),
#   6. saves the runner output, logcat, the frames (CODEBRIX_UIREQS_FRAME_SAVE) and a summary under --out,
#   7. stops the emulator (unless --keep-emulator).
# Lock hygiene: every long-lived child (the emulator, the adb server, logcat, the dotnet builds and their build
# servers) is started with the lock fd 9 closed, so nothing outlives the run holding the build lock; an EXIT trap
# (also on INT/TERM/HUP) removes the port forward, unlocks the rotation and stops the emulator (unless
# --keep-emulator) on EVERY way out, including a failed device build.
# Exit status: 0 when every selected scenario passed or was skipped (other orientation / pending), 1 otherwise,
# 2 for setup failures.
#
# usage: build/test-scripts/android-uireqs-run.sh [--group "Harness Layout ..."|all] [--orientation portrait|landscape|both]
#                                                 [--out DIR] [--keep-emulator] [--port 5600] [--no-lock] [--density DPI]
# --density DPI runs at another display density (`wm density DPI`, e.g. 420 = 2.625, 560 = 3.5; reset afterwards):
#   the scenarios that state sizes in device pixels assume density 1 and fail there by design - what such a run
#   proves is the layout-replay GEOMETRY AUDIT (every named element's native view = its Core rect within 1 px),
#   whose failures read "Layout replay:".
set -u
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
group=Harness
orientations="portrait landscape"
port=5600
keep=0
nolock=0
density=""
out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --group) group=$2; shift 2 ;;
    --orientation) case "$2" in both) orientations="portrait landscape" ;; *) orientations=$2 ;; esac; shift 2 ;;
    --out) out=$2; shift 2 ;;
    --keep-emulator) keep=1; shift ;;
    --port) port=$2; shift 2 ;;
    --no-lock) nolock=1; shift ;;
    --density) density=$2; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done
out=${out:-$repo/artifacts/uireqs/$(date +%Y%m%d-%H%M%S)-${group// /-}}
mkdir -p "$out"
export MSBUILDDISABLENODEREUSE=1
adb="${ANDROID_HOME:-$HOME/Android/Sdk}/platform-tools/adb"
serial="emulator-$port"
# Other adb devices may be attached (a developer's phone or tablet): adb without -s must never address them.
export ANDROID_SERIAL=$serial
lock=${CODEBRIX_ANDROID_BUILD_LOCK:-$HOME/ClaudeHome/android-buildout-work/build.lock}
# [AP9-2] Android-only groups whose claims need the system animations ON (native indicators honour the animator duration
# scale; Jeremy 2026-09-26 Option A). Every other group runs with the scale at 0.
animator_on_groups="AndroidAnimatorOn"
log() { echo "[$(date +%H:%M:%S)] $*" | tee -a "$out/run.log"; }

if [ $nolock -eq 0 ]; then
  exec 9>>"$lock"
  log "waiting for the build lock $lock"
  flock 9
fi

dsh() { "$adb" -s "$serial" shell "$@"; }
logcat_pid=""
emulator_up=0
finished=0
# Every way out: no forward left, rotation unlocked, density reset, emulator stopped unless --keep-emulator.
cleanup() {
  local rc=$?
  [ -n "$logcat_pid" ] && kill "$logcat_pid" 2>/dev/null
  [ $finished -eq 1 ] && return
  if [ $emulator_up -eq 1 ]; then
    timeout 10 "$adb" -s "$serial" forward --remove tcp:47300 >/dev/null 2>&1
    timeout 10 "$adb" -s "$serial" shell cmd window user-rotation lock 0 >/dev/null 2>&1
    timeout 10 "$adb" -s "$serial" shell settings put system user_rotation 0 >/dev/null 2>&1
    timeout 10 "$adb" -s "$serial" shell settings put global animator_duration_scale 0 >/dev/null 2>&1
    [ -n "$density" ] && timeout 10 "$adb" -s "$serial" shell wm density reset >/dev/null 2>&1
  fi
  if [ $keep -eq 0 ]; then
    log "exit $rc before the end of the run - stopping the emulator"
    "$here/android-uireqs-avd.sh" stop --port "$port" 9>&- 2>&1 | tee -a "$out/run.log"
  fi
}
trap cleanup EXIT
trap 'exit 130' INT TERM HUP

# The display size as a screencap reports it (raw screencap header: width, height).
display_size() { timeout 10 "$adb" -s "$serial" exec-out screencap 2>/dev/null | head -c 8 | od -An -tu4 | awk '{print $1 "x" $2}'; }

log "UIReqs run: group=$group orientations=[$orientations] out=$out"
# The adb server is a long-lived daemon: never let it inherit the lock fd.
timeout 20 "$adb" start-server >/dev/null 2>&1 9>&-
"$here/android-uireqs-avd.sh" start --port "$port" --log "$out/emulator.log" 9>&- 2>&1 | tee -a "$out/run.log"
[ "${PIPESTATUS[0]}" -eq 0 ] || { log "emulator did not start"; exit 2; }
emulator_up=1
dsh settings put global window_animation_scale 0
dsh settings put global transition_animation_scale 0
dsh settings put global animator_duration_scale 0
dsh settings put system font_scale 1.0
dsh settings put system show_touches 0
dsh settings put system accelerometer_rotation 0
dsh settings put system screen_off_timeout 2147483647
dsh svc power stayon true
dsh cmd uimode night no >/dev/null
dsh wm size reset
dsh wm density reset
[ -n "$density" ] && dsh wm density "$density"
log "device settings fixed (animations 0, font scale 1.0, night mode off, touches hidden, awake, rotation locked)"

# The ABI is named explicitly (the AVD's own), never detected: a Debug build without a target asks the DEFAULT adb device
# for its ABI, and an APK built for another device's ABI (arm64-only) is "up to date" for the next -t:Install and aborts
# on this x86_64 AVD ("No assemblies found ... Fast Deployment").
abi=$(dsh getprop ro.product.cpu.abi | tr -d '\r')
case "$abi" in
  x86_64) rid=android-x64 ;;
  arm64-v8a) rid=android-arm64 ;;
  *) log "unexpected device ABI '$abi' on $serial"; exit 2 ;;
esac
log "installing the scenario app (Debug, -t:Install, $rid)"
if ! dotnet build "$repo/tests/CodeBrix.Android.UIReqs.Device/CodeBrix.Android.UIReqs.Device.csproj" -c Debug -t:Install "-p:AdbTarget=-s $serial" "-p:RuntimeIdentifier=$rid" -nologo > "$out/device-build.log" 2>&1 9>&-; then
  log "device app build/install FAILED (see device-build.log)"; grep -E 'error' "$out/device-build.log" | sort -u | head -20 | tee -a "$out/run.log"; exit 2
fi
installed_abi=$(dsh pm dump com.codebrix.uireqs | sed -n 's/^ *primaryCpuAbi=//p' | head -1 | tr -d '\r')
if [ "$installed_abi" != "$abi" ]; then
  log "the installed scenario app runs as '$installed_abi', not the device's '$abi' - refusing to run"; exit 2
fi
if ! dotnet build "$repo/tests/CodeBrix.Android.UIReqs/CodeBrix.Android.UIReqs.csproj" -c Debug -nologo > "$out/host-build.log" 2>&1 9>&-; then
  log "host runner build FAILED (see host-build.log)"; exit 2
fi
runner="$repo/tests/CodeBrix.Android.UIReqs/bin/Debug/net10.0/CodeBrix.Android.UIReqs"

failed=0
for orientation in $orientations; do
for g in $group; do
  filter=()
  [ "$g" != all ] && filter=(--filter-namespace "CodeBrix.Android.UIReqs.Features.$g")
  case "$orientation" in portrait) rotation=0; Orientation=Portrait ;; landscape) rotation=1; Orientation=Landscape ;; *) log "bad orientation $orientation"; exit 2 ;; esac
  tag="$Orientation-$g"
  dsh settings put system user_rotation $rotation
  # API 37 ignores user_rotation alone; the window manager's rotation lock really rotates the display.
  dsh cmd window user-rotation lock $rotation
  case "$rotation" in 0) want=1080x1920 ;; *) want=1920x1080 ;; esac
  [ -n "$density" ] && want=""   # only the default size is known here; a --density run is not rotation-checked
  if [ -n "$want" ]; then
    size=""
    for _ in $(seq 1 20); do size=$(display_size); [ "$size" = "$want" ] && break; sleep 0.5; done
    if [ "$size" != "$want" ]; then log "$tag: the display is '$size', expected $want - rotation did not apply"; exit 2; fi
    log "$tag: display $size"
  fi
  dsh am force-stop com.codebrix.uireqs
  animator_on=0
  case " $animator_on_groups " in *" $g "*) animator_on=1 ;; esac
  if [ $animator_on -eq 1 ]; then
    dsh settings put global animator_duration_scale 1
    log "$tag: animator duration scale 1 for this group (restored to 0 after it)"
  fi
  "$adb" -s "$serial" logcat -c
  "$adb" -s "$serial" logcat -v brief > "$out/$tag.logcat.txt" 2>&1 9>&- &
  logcat_pid=$!
  dsh am start -W -n com.codebrix.uireqs/com.codebrix.uireqs.MainActivity | grep -E 'Status|TotalTime' | sed 's/^/  /' | tee -a "$out/run.log"
  "$adb" -s "$serial" forward tcp:47300 tcp:47300 >/dev/null
  log "$tag: running the host runner"
  UIREQS_SERIAL=$serial UIREQS_HOST_PORT=47300 UIREQS_ORIENTATION=$Orientation CODEBRIX_UIREQS_FRAME_SAVE="$out/frames" \
    "$runner" "${filter[@]}" --results-directory "$out/results-$tag" --output Detailed > "$out/$tag.log" 2>&1
  rc=$?
  kill $logcat_pid 2>/dev/null
  logcat_pid=""
  "$adb" -s "$serial" forward --remove tcp:47300 >/dev/null 2>&1
  "$adb" -s "$serial" exec-out screencap -p > "$out/$tag.last.png"
  if [ $animator_on -eq 1 ]; then
    dsh am force-stop com.codebrix.uireqs
    dsh settings put global animator_duration_scale 0
  fi
  summary=$(grep -E '^\s+(total|failed|succeeded|skipped):' "$out/$tag.log" | tr -s ' ' | tr '\n' ' ')
  log "$tag: exit $rc; $summary"
  grep -E '^\s*(failed|skipped) ' "$out/$tag.log" | sed 's/^/    /' | tee -a "$out/run.log"
  [ $rc -eq 0 ] || failed=1
done
done
dsh cmd window user-rotation lock 0
dsh settings put system user_rotation 0
[ -n "$density" ] && dsh wm density reset

finished=1
if [ $keep -eq 0 ]; then
  "$here/android-uireqs-avd.sh" stop --port "$port" 9>&- 2>&1 | tee -a "$out/run.log"
fi
log "UIReqs: $([ $failed -eq 0 ] && echo PASS || echo FAIL) (output: $out)"
exit $failed
