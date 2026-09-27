#!/bin/bash
# paste-always-compile.sh - the paste-always compile test (plan 4.10, AP1.7; every corpus page since AP8-T).
#
# Builds every paste-always head under tests/PasteAlways/<App>/ (or only the apps named on the
# command line) for net10.0-android36.1 and reports one line per corpus app. An app PASSES when
#   1. its pasted files are unchanged (tests/PasteAlways/<App>/pasted-files.sha256), and
#   2. its head builds with 0 errors and 0 warnings other than CBAND diagnostics.
# CBAND warnings (the CodeBrix.Android analyzer and XAML scan: constructs Android accepts and ignores) are
# COUNTED, never failures: the CBAND column, and <log dir>/cband-counts.tsv (per app and per id) and
# <log dir>/cband-findings.txt (every finding, file and line).
# Exit status: 0 when every app passes, 1 otherwise.
#
# usage:  build/test-scripts/paste-always-compile.sh [-c Debug|Release] [App ...]
# env:    CODEBRIX_ANDROID_BUILD_LOCK  a lock file; when set, every build runs under `flock -o <lock>`
#         PASTE_ALWAYS_LOG_DIR         where the build logs go (default: artifacts/paste-always/)
set -u
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
config=Debug
if [ "${1:-}" = "-c" ]; then config=$2; shift 2; fi
logdir=${PASTE_ALWAYS_LOG_DIR:-$repo/artifacts/paste-always}
mkdir -p "$logdir"
lock=${CODEBRIX_ANDROID_BUILD_LOCK:-}
export MSBUILDDISABLENODEREUSE=1
ids="CBAND0001 CBAND0002 CBAND0003 CBAND0004 CBAND0005 CBAND0006 CBAND0007 CBAND0008"

apps=("$@")
if [ ${#apps[@]} -eq 0 ]; then
  for d in "$repo"/tests/PasteAlways/*/; do
    ls "$d"*.PasteAlways.csproj >/dev/null 2>&1 && apps+=("$(basename "$d")")
  done
fi

run() { if [ -n "$lock" ]; then flock -o "$lock" "$@"; else "$@"; fi; }

failed=0
summary="$logdir/summary.txt"
counts="$logdir/cband-counts.tsv"
findings="$logdir/cband-findings.txt"
printf 'app\ttotal' > "$counts"; for id in $ids; do printf '\t%s' "$id" >> "$counts"; done; printf '\n' >> "$counts"
: > "$findings"
printf '%-28s %-6s %-10s %-9s %-6s %-9s %s\n' PAGE RESULT PASTED WARNINGS CBAND ERRORS LOG | tee "$summary"
for app in "${apps[@]}"; do
  dir="$repo/tests/PasteAlways/$app"
  proj=$(ls "$dir"/*.PasteAlways.csproj 2>/dev/null | head -1)
  log="$logdir/$app.$config.log"
  if [ -z "$proj" ]; then
    printf '%-28s %-6s %s\n' "$app" FAIL "no tests/PasteAlways/$app/*.PasteAlways.csproj" | tee -a "$summary"; failed=1; continue
  fi

  # 1. The pasted files are the recorded verbatim copies.
  pasted=ok; count=0
  while read -r hash file _; do
    [ -z "$hash" ] && continue
    count=$((count + 1))
    actual=$(sha256sum "$dir/$file" 2>/dev/null | cut -d' ' -f1)
    if [ "$actual" != "$hash" ]; then pasted="CHANGED:$file"; fi
  done < "$dir/pasted-files.sha256"
  [ "$pasted" = ok ] && pasted="ok($count)"

  # 2. The head builds: 0 errors, 0 warnings other than CBAND. (MSBuild prints each warning twice - where it
  #    happens and again in the closing summary - so findings are de-duplicated on the whole line without the
  #    trailing [project] tag.)
  run dotnet build "$proj" -c "$config" -nologo > "$log" 2>&1
  rc=$?
  warnings=$(grep -E '^ +[0-9]+ Warning\(s\)' "$log" | tail -1 | awk '{print $1}')
  errors=$(grep -E '^ +[0-9]+ Error\(s\)' "$log" | tail -1 | awk '{print $1}')
  app_findings=$(grep -E ': warning CBAND[0-9]{4}:' "$log" | sed -E 's/ \[[^]]*\]$//' | sort -u)
  cband=0
  [ -n "$app_findings" ] && cband=$(printf '%s\n' "$app_findings" | wc -l)
  other=x
  [ -n "${warnings:-}" ] && other=$((warnings - cband))
  printf '%s' "$app" >> "$counts"; printf '\t%s' "$cband" >> "$counts"
  for id in $ids; do
    n=0; [ -n "$app_findings" ] && n=$(printf '%s\n' "$app_findings" | grep -c ": warning $id:")
    printf '\t%s' "$n" >> "$counts"
  done
  printf '\n' >> "$counts"
  [ -n "$app_findings" ] && printf '%s\n' "$app_findings" | sed "s|^|$app: |" >> "$findings"

  result=PASS
  if [ $rc -ne 0 ] || [ "${errors:-x}" != 0 ] || [ "$other" != 0 ] || [[ "$pasted" != ok* ]]; then result=FAIL; failed=1; fi
  printf '%-28s %-6s %-10s %-9s %-6s %-9s %s\n' "$app" "$result" "$pasted" "$other" "$cband" "${errors:-?}" "$log" | tee -a "$summary"
done

echo "paste-always: $([ $failed -eq 0 ] && echo PASS || echo FAIL) (${#apps[@]} apps, $config; WARNINGS = warnings other than CBAND; CBAND counts in $counts)" | tee -a "$summary"
exit $failed
