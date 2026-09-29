#!/bin/bash
# android-uireqs-avd.sh - the CodeBrix.Android agent / test AVD: create, start, stop, status.
#
# create (the default): creates the AVD CodeBrix_Agent_15inch if it does not exist yet: the 15-inch HD
#   profile (1080x1920, 160 dpi = density 1, hardware keyboard, 4 GB RAM) on the installed API 37 x86_64
#   google_apis system image. The profile values are written into the AVD's config.ini, so the script does
#   not depend on a device definition in ~/.android/devices.xml. It never modifies, deletes or starts any
#   other AVD, and it never touches an existing CodeBrix_Agent_15inch (re-running it is a no-op).
#
# start: creates the AVD if needed, then starts it headless on --port (default 5600 = serial emulator-5600;
#   5554, the default port, is refused because it may be Jeremy's) and waits until sys.boot_completed=1 and
#   the package manager answers. It prints the boot time and exits 0.
#   - Cold boot is the default (--cold = -no-snapshot-load -no-snapshot-save): no Quick Boot snapshot is
#     ever loaded or written. Loading a saved default_boot snapshot is what made AVDs on this machine crash
#     (SIGSEGV) on every start after the first one; a cold boot of this AVD takes about 15-20 s headless.
#     --snapshot opts back into the emulator's normal snapshot load/save (not recommended).
#   - Stale lock files (*.lock in the AVD folder, for example hardware-qemu.ini.lock or multiinstance.lock
#     left behind by a crashed emulator) are removed ONLY when no emulator/qemu process for this AVD runs.
#   - When the serial is already online and runs this AVD, it reports that and exits 0 without starting a
#     second instance; when it runs another AVD, or the port is bound by something else, it exits 2.
#   - The emulator is started with file descriptors 3-9 CLOSED, so it never inherits a caller's lock fd
#     (android-uireqs-run.sh holds the build lock on fd 9; an emulator that inherited it kept the Android
#     track's build lock taken for as long as it ran). The adb server is started the same way first.
#   - Every adb call of the boot wait is bounded (`timeout 10`): an adb that hangs on a half-booted emulator
#     cannot hang the script past --timeout.
#   - An EXIT trap stops the emulator this invocation started if the script ends (error, timeout, signal)
#     before the boot completed; after a completed boot the emulator is the caller's to stop.
# stop: stops the emulator on --port only when it runs this AVD (`adb emu kill`), waits for this AVD's
#   emulator process to exit (60 s, then SIGTERM to that process only), and reports. Any other emulator
#   is never touched. A no-op (exit 0) when this AVD is not running.
# status: prints whether this AVD runs, its processes, and its lock files.
#
# usage: build/test-scripts/android-uireqs-avd.sh [create|start|stop|status] [--name NAME] [--port PORT]
#                                                 [--cold|--snapshot] [--window] [--log FILE] [--timeout SECS]
# env:   ANDROID_HOME      the Android SDK (default ~/Android/Sdk)
#        ANDROID_AVD_HOME  the AVD folder (default ~/.android/avd)
# The caller takes the Android track's build lock (flock) around start/stop when it needs one.
# Exit status: 0 ok; 1 boot timeout / stop failure; 2 setup failure (missing image, refused port, foreign AVD).
set -u
action=create
case "${1:-}" in create|start|stop|status) action=$1; shift ;; esac
name=CodeBrix_Agent_15inch
port=5600
cold=1
window=0
emulog=""
timeout=300
while [ $# -gt 0 ]; do
  case "$1" in
    --name) name=$2; shift 2 ;;
    --port) port=$2; shift 2 ;;
    --cold) cold=1; shift ;;
    --snapshot) cold=0; shift ;;
    --window) window=1; shift ;;
    --log) emulog=$2; shift 2 ;;
    --timeout) timeout=$2; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done
[ -n "$name" ] || { echo "FAIL: empty AVD name" >&2; exit 2; }
sdk=${ANDROID_HOME:-$HOME/Android/Sdk}
image="system-images;android-37.0;google_apis;x86_64"
avdmanager="$sdk/cmdline-tools/latest/bin/avdmanager"
emulator="$sdk/emulator/emulator"
adb="$sdk/platform-tools/adb"
avd_home=${ANDROID_AVD_HOME:-$HOME/.android/avd}
avd_dir="$avd_home/$name.avd"
serial=emulator-$port

log() { echo "[$(date +%H:%M:%S)] $*"; }

# ---------------------------------------------------------------- create
create_avd() {
  if "$emulator" -list-avds 2>/dev/null | grep -qx "$name"; then
    echo "AVD $name exists - nothing to do"
    return 0
  fi
  if [ ! -d "$sdk/system-images/android-37.0/google_apis/x86_64" ]; then
    echo "FAIL: the system image $image is not installed (this script never installs software)" >&2
    return 2
  fi
  if [ -e "$avd_home/$name.ini" ] || [ -e "$avd_dir" ]; then
    echo "FAIL: $avd_home/$name.ini or .avd exists but the emulator does not list it; not touching it" >&2
    return 2
  fi

  echo "creating AVD $name ($image)"
  echo no | "$avdmanager" create avd --name "$name" --package "$image" --tag google_apis --abi x86_64 > /dev/null || return 2

  local config="$avd_dir/config.ini"
  set_config() {
    if grep -q "^$1=" "$config"; then sed -i "s|^$1=.*|$1=$2|" "$config"; else echo "$1=$2" >> "$config"; fi
  }
  set_config hw.device.name "CodeBrix 15-inch HD (agent)"
  set_config hw.lcd.width 1080
  set_config hw.lcd.height 1920
  set_config hw.lcd.density 160
  set_config hw.keyboard yes
  set_config hw.ramSize 4096
  set_config hw.gpu.enabled yes
  set_config hw.gpu.mode auto
  set_config disk.dataPartition.size 6G
  set_config showDeviceFrame no
  # Cold boot also when the AVD is started from somewhere else (Android Studio's Device Manager).
  set_config fastboot.forceColdBoot yes
  set_config fastboot.forceFastBoot no

  "$emulator" -list-avds | grep -qx "$name" || { echo "FAIL: $name was not created" >&2; return 2; }
  echo "AVD $name created"
}

# ---------------------------------------------------------------- helpers
# PIDs of the emulator launcher / qemu processes running THIS AVD (exact "-avd NAME" argument match; the
# emulator's own "-kill PID" helper is not included). Reads the process table with ps - never pgrep -f.
avd_pids() {
  ps -eo pid=,args= | awk -v n="$name" '
    $2 ~ /(qemu-system-[A-Za-z0-9_-]+|\/emulator\/emulator)$/ {
      for (i = 3; i < NF; i++) if ($i == "-avd" && $(i + 1) == n) { print $1; break }
    }'
}
# Every adb call is bounded: a half-booted emulator can leave adb waiting forever.
online() { [ "$(timeout 10 "$adb" -s "$serial" get-state 2>/dev/null)" = device ]; }
serial_avd() { timeout 10 "$adb" -s "$serial" emu avd name 2>/dev/null | head -1 | tr -d '\r'; }
booted() { [ "$(timeout 10 "$adb" -s "$serial" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ]; }
port_bound() { ss -Htln 2>/dev/null | awk '{print $4}' | grep -q -E "[:.]($port|$((port + 1)))\$"; }

remove_stale_locks() {
  local pids
  pids=$(avd_pids)
  if [ -n "$pids" ]; then
    log "not removing lock files: an emulator for $name is running (pid $(echo $pids))"
    return 1
  fi
  case "$avd_dir" in */"$name".avd) ;; *) return 1 ;; esac
  [ -d "$avd_dir" ] || return 0
  find "$avd_dir" -maxdepth 1 -type f -name '*.lock' -print | while read -r f; do
    rm -f -- "$f" && log "removed stale lock file $(basename "$f")"
  done
}

# ---------------------------------------------------------------- start
booting_pid=""
# EXIT trap while booting: stop the emulator this invocation started (only this AVD's processes; stop_avd).
boot_guard() {
  [ -n "$booting_pid" ] || return 0
  log "boot not completed - stopping the emulator this script started (pid $booting_pid)"
  booting_pid=""
  stop_avd > /dev/null 2>&1
}

start_avd() {
  if [ "$port" = 5554 ] || [ "$port" = 5555 ]; then
    echo "FAIL: refusing port $port (the default emulator port may be Jeremy's)" >&2; return 2
  fi
  create_avd > /dev/null || return 2
  if online; then
    local running
    running=$(serial_avd)
    if [ "$running" = "$name" ]; then log "$serial already runs $name - not starting a second instance"; return 0; fi
    echo "FAIL: $serial runs AVD '$running', not '$name' - not touching it" >&2; return 2
  fi
  local pids
  # [AP7-B AdvancedTextEdit, coordinator 2026-09-27 08:19] A start that follows a stop by seconds could hang in the cold
  # boot (seen 2026-09-26 21:56 and 2026-09-27 08:12): wait (up to 60 s, polling every 2 s) until no emulator/qemu process
  # of THIS AVD is alive and its ports are free. Never kills anything; if one is still there after 60 s, it is printed and
  # the checks below decide as before.
  local waited=0
  while { [ -n "$(avd_pids)" ] || port_bound; } && [ $waited -lt 60 ]; do
    [ $waited -eq 0 ] && log "waiting for the previous $name emulator to exit (pid $(echo $(avd_pids)); ports $port/$((port + 1)) bound: $(port_bound && echo yes || echo no))"
    sleep 2; waited=$((waited + 2))
  done
  [ $waited -gt 0 ] && log "waited ${waited} s for the previous emulator to exit$([ -n "$(avd_pids)" ] && echo " - still running: pid $(echo $(avd_pids))")"
  pids=$(avd_pids)
  if [ -n "$pids" ]; then
    echo "FAIL: an emulator process for $name is running (pid $(echo $pids)) but $serial is not online;" \
         "run '$0 stop --name $name --port $port' first" >&2
    return 2
  fi
  if port_bound; then
    echo "FAIL: port $port or $((port + 1)) is already bound by another process" >&2; return 2
  fi
  remove_stale_locks
  # The adb server is a long-lived daemon: start it with no inherited lock fd (see the header).
  timeout 20 "$adb" start-server > /dev/null 2>&1 3>&- 4>&- 5>&- 6>&- 7>&- 8>&- 9>&-

  local args=(-avd "$name" -port "$port" -no-audio -no-boot-anim)
  [ $window -eq 1 ] || args+=(-no-window)
  if [ $cold -eq 1 ]; then args+=(-no-snapshot-load -no-snapshot-save); fi
  [ -n "$emulog" ] || emulog="${TMPDIR:-/tmp}/codebrix-avd-$name-$port.log"
  mkdir -p "$(dirname "$emulog")"
  log "starting $name on $serial ($([ $cold -eq 1 ] && echo cold boot || echo snapshot boot)): emulator ${args[*]}"
  local t0 pid
  t0=$(date +%s)
  # fds 3-9 closed: the emulator must never inherit (and so hold) a caller's lock fd.
  nohup "$emulator" "${args[@]}" > "$emulog" 2>&1 < /dev/null 3>&- 4>&- 5>&- 6>&- 7>&- 8>&- 9>&- &
  pid=$!
  disown $pid 2>/dev/null
  # Until the boot completes, any way out of this script stops the emulator it just started.
  booting_pid=$pid
  trap boot_guard EXIT
  trap 'exit 130' INT TERM HUP
  while :; do
    if ! kill -0 "$pid" 2>/dev/null; then
      log "FAIL: the emulator exited during boot (see $emulog)"; tail -5 "$emulog" >&2; return 1
    fi
    if booted && timeout 10 "$adb" -s "$serial" shell pm path android > /dev/null 2>&1; then break; fi
    if [ $(( $(date +%s) - t0 )) -ge "$timeout" ]; then
      log "FAIL: $serial did not boot within $timeout s (see $emulog); stopping it"; return 1
    fi
    sleep 2
  done
  if [ "$(serial_avd)" != "$name" ]; then
    log "FAIL: $serial came up running '$(serial_avd)', not '$name'"; return 2
  fi
  booting_pid=""
  trap - EXIT INT TERM HUP
  log "$name booted on $serial in $(( $(date +%s) - t0 )) s (emulator pid $pid, log $emulog)"
}

# ---------------------------------------------------------------- stop
stop_avd() {
  if [ "$port" = 5554 ] || [ "$port" = 5555 ]; then
    echo "FAIL: refusing port $port (the default emulator port may be Jeremy's)" >&2; return 2
  fi
  local pids t0
  pids=$(avd_pids)
  if online; then
    local running
    running=$(serial_avd)
    if [ "$running" != "$name" ]; then
      log "not stopping $serial: it runs '$running', not '$name'"
      [ -z "$pids" ] && return 0
    else
      log "stopping $name on $serial"
      timeout 15 "$adb" -s "$serial" emu kill > /dev/null 2>&1
    fi
  fi
  if [ -z "$pids" ]; then log "$name is not running"; return 0; fi
  t0=$(date +%s)
  while [ -n "$(avd_pids)" ] && [ $(( $(date +%s) - t0 )) -lt 60 ]; do sleep 1; done
  pids=$(avd_pids)
  if [ -n "$pids" ]; then
    # Only processes whose command line is exactly "-avd $name" - this AVD's own emulator.
    log "$name still running after 60 s - sending SIGTERM to pid $(echo $pids)"
    kill $pids 2>/dev/null
    for _ in $(seq 1 20); do [ -z "$(avd_pids)" ] && break; sleep 1; done
  fi
  if [ -n "$(avd_pids)" ]; then log "FAIL: $name is still running (pid $(echo $(avd_pids)))"; return 1; fi
  for _ in $(seq 1 15); do port_bound || break; sleep 1; done
  log "$name stopped ($(( $(date +%s) - t0 )) s)"
}

status_avd() {
  local pids
  pids=$(avd_pids)
  echo "AVD $name: $("$emulator" -list-avds 2>/dev/null | grep -qx "$name" && echo exists || echo missing)"
  echo "processes: ${pids:-none}"
  if online; then echo "$serial: online, runs '$(serial_avd)', boot_completed=$(booted && echo 1 || echo 0)"; else echo "$serial: offline"; fi
  echo "lock files: $(cd "$avd_dir" 2>/dev/null && ls *.lock 2>/dev/null | tr '\n' ' ')"
}

case $action in
  create) create_avd ;;
  start) start_avd ;;
  stop) stop_avd ;;
  status) status_avd ;;
esac
