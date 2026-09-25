#!/bin/bash
# Compares two folders of saved UIReqs frames (written by a UIReqs run with CODEBRIX_UIREQS_FRAME_SAVE=<folder>).
#
#   build/test-scripts/compare-uireqs-frames.sh <baseline-folder> <current-folder> [--threshold <n>] [--report <file>] [--informational <Group>]...
#   build/test-scripts/compare-uireqs-frames.sh --self-test <baseline-folder> [--work <folder>]
#
# Runs tools/UIReqsFrameCompare. Every entry in uireqs-frame-compare.informational (next to this script) is
# always passed as --informational: a whole group (<Group>) or one feature (<Group>/<feature>). Frames they
# cover are reported but never fail the run. Everything else is strict: pixel-identical (within --threshold,
# default 0) and the same set of frames.
# Diff images land under <current-folder>/_diff/. Exit code: 0 = pass, 1 = differences, 2 = usage error.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
INFORMATIONAL_FILE="$SCRIPT_DIR/uireqs-frame-compare.informational"

informational_args=()
if [ -f "$INFORMATIONAL_FILE" ]; then
	while IFS= read -r line || [ -n "$line" ]; do
		group="${line%%#*}"
		group="$(echo "$group" | tr -d '[:space:]')"
		if [ -n "$group" ]; then
			informational_args+=(--informational "$group")
		fi
	done < "$INFORMATIONAL_FILE"
fi

exec dotnet run --project "$REPO_ROOT/tools/UIReqsFrameCompare" -c Release -- "${informational_args[@]}" "$@"
