#!/bin/bash
# CodeBrix.Android: pack every package and run the package gates (Linux).
#
#   build/pack.sh [-c Release|Debug] [--no-build] [--version 1.x.y.z]
#
# 1. Builds CodeBrix.Android.slnx in the configuration (skipped with --no-build: the outputs must exist).
# 2. Runs the pack driver build/nuget/CodeBrix.Android.Pack.proj: the framework package and one package per
#    add-in listed there, all with ONE date-stamped version (or --version, to re-pack an existing one), into
#    artifacts/packages/<configuration>/<version>/ (git-ignored), then the three package gates
#    (report: package-gates.txt beside the packages). A failed gate fails the script.
# Publishing is Jeremy's: nothing here pushes a package anywhere.
#
# Build lock: every dotnet command runs under `flock -o $CODEBRIX_ANDROID_BUILD_LOCK` when that variable is set
# (the Android track sets it to ~/ClaudeHome/android-buildout-work/build.lock); a caller that already holds the
# lock passes --no-lock.
set -euo pipefail

CONFIG=Release
BUILD=1
VERSION=
USE_LOCK=1
while [ $# -gt 0 ]; do
  case "$1" in
    -c|--configuration) CONFIG="$2"; shift 2 ;;
    --no-build) BUILD=0; shift ;;
    --version) VERSION="$2"; shift 2 ;;
    --no-lock) USE_LOCK=0; shift ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "pack.sh: unknown option $1" >&2; exit 2 ;;
  esac
done

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"

run() {
  if [ "$USE_LOCK" = 1 ] && [ -n "${CODEBRIX_ANDROID_BUILD_LOCK:-}" ]; then
    flock -o "$CODEBRIX_ANDROID_BUILD_LOCK" "$@"
  else
    "$@"
  fi
}

if [ "$BUILD" = 1 ]; then
  echo "pack.sh: building CodeBrix.Android.slnx ($CONFIG)"
  run dotnet build CodeBrix.Android.slnx -c "$CONFIG" -nologo
fi

ARGS=(build build/nuget/CodeBrix.Android.Pack.proj -c "$CONFIG" -nologo)
if [ -n "$VERSION" ]; then
  ARGS+=("-p:BuildVersion=$VERSION")
fi
echo "pack.sh: packing ($CONFIG)"
run dotnet "${ARGS[@]}"

LATEST="$(ls -1dt artifacts/packages/"$CONFIG"/*/ 2>/dev/null | head -1)"
echo "pack.sh: packages in ${LATEST:-artifacts/packages/$CONFIG/}"
ls -1 "$LATEST"*.nupkg 2>/dev/null | sed 's|^|  |'
