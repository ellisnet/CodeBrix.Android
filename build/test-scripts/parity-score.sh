#!/bin/bash
# parity-score.sh - the CodeBrix.Android parity score (AP8), published per build.
#
# Runs tools/CodeBrix.Android.ParityScore over
#   - the pinned build's extracted Core assemblies:  artifacts/intake/<pin>/lib/*.Core.dll
#   - the built CodeBrix.Android assemblies:          src/**/bin/<config>/net10.0-android36.1/CodeBrix.Android*.dll
# and writes the report to artifacts/parity/<pin>/<config>/:
#   parity-summary.txt                 the two scores in one page (also printed)
#   parity-notimplemented.tsv          (a) NotImplemented members per public Core type
#   parity-notimplemented-members.tsv      every NotImplemented member and why (marked / throws / raises)
#   parity-declined.tsv                (b) per native (element, handler): properties in scope, mapped, explained, declined
#   parity-declined-properties.tsv         every property row (mapped / partial / explained / declined)
#   parity-templated.tsv                   registered elements served by the templated fallback or Core's path
# The explained list is tools/CodeBrix.Android.ParityScore/declined-explained.tsv.
# Build the solution first (the gate recipe does); the tool is built here, under the lock, only when it is missing.
#
# usage:  build/test-scripts/parity-score.sh [-c Debug|Release]
# env:    CODEBRIX_ANDROID_BUILD_LOCK  a lock file; when set, a build of the tool runs under `flock -o <lock>`
# Exit status: the tool's (0 = report written).
set -u
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
config=Debug
if [ "${1:-}" = "-c" ]; then config=$2; shift 2; fi
lock=${CODEBRIX_ANDROID_BUILD_LOCK:-}
export MSBUILDDISABLENODEREUSE=1

pin=$(sed -n 's:.*<CodeBrixPlatformVersion>\(.*\)</CodeBrixPlatformVersion>.*:\1:p' "$repo/build/PlatformPin.props" | head -1)
intake="$repo/artifacts/intake/$pin/lib"
out="$repo/artifacts/parity/$pin/$config"
tool_project="$repo/tools/CodeBrix.Android.ParityScore/CodeBrix.Android.ParityScore.csproj"
tool="$repo/tools/CodeBrix.Android.ParityScore/bin/$config/net10.0/CodeBrix.Android.ParityScore.dll"

if [ ! -d "$intake" ]; then
  echo "parity-score: no intake at $intake (build the solution first)" >&2
  exit 1
fi

if [ ! -f "$tool" ]; then
  if [ -n "$lock" ]; then
    flock -o "$lock" dotnet build "$tool_project" -c "$config" -nologo -v q || exit 1
  else
    dotnet build "$tool_project" -c "$config" -nologo -v q || exit 1
  fi
fi

args=()
for dir in "$repo"/src/*/bin/"$config"/net10.0-android36.1 "$repo"/src/AddIns/*/bin/"$config"/net10.0-android36.1; do
  [ -d "$dir" ] && args+=(--android "$dir")
done
if [ ${#args[@]} -eq 0 ]; then
  echo "parity-score: no built CodeBrix.Android assemblies for $config (build the solution first)" >&2
  exit 1
fi

dotnet "$tool" --intake-lib "$intake" "${args[@]}" --out "$out" \
  --label "pin: CodeBrix.Platform $pin (build/PlatformPin.props)" --label "configuration: $config"
