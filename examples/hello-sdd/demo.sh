#!/usr/bin/env bash
# hello-sdd — generate a minimal SDD project, show the dashboard, then CHECK
# that examples/hello-sdd/dashboard.txt matches the tool's REAL output.
# Requires .NET SDK 10. Runnable from anywhere.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build "$REPO/src/Sdd.Cli" -v q --nologo >/dev/null

# CLI resolved from the repo — no global install required
sdd_cli() { dotnet run --project "$REPO/src/Sdd.Cli" --no-build -- "$@"; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# `sdd init` writes in the CURRENT directory: create the project folder first.
mkdir -p "$WORK/hello-sdd"
cd "$WORK/hello-sdd"

sdd_cli init --projet hello-sdd
sdd_cli new HELLO

# mark the spec as in flight (same as editing AGENT_STATE + Statut by hand)
edit_in_place() {
  sed -i.bak "$1" "$2"
  rm -f "$2.bak"
}
edit_in_place 's/\*\*Statut\*\* : Brouillon/**Statut** : En phase/' docs/specs/SPEC-HELLO.md
edit_in_place 's/| Phase active | — |/| Phase active | P1 |/' docs/AGENT_STATE.md
edit_in_place 's/| Spec de référence | — |/| Spec de référence | SPEC-HELLO |/' docs/AGENT_STATE.md

echo
sdd_cli status
echo
sdd_cli lint
echo

# The documented dashboard must not be able to lie: it is produced by the tool,
# never written by hand. The test suite runs this script (and CI runs the suite),
# so any drift fails the gate.
sdd_cli status > "$WORK/status.reel.txt"
if [ -f "$HERE/dashboard.txt" ]; then
  if diff -u "$HERE/dashboard.txt" "$WORK/status.reel.txt" >/dev/null; then
    echo "OK — dashboard.txt matches the real output of sdd status."
  else
    echo "FAIL — dashboard.txt no longer matches the real output:" >&2
    diff -u "$HERE/dashboard.txt" "$WORK/status.reel.txt" >&2 || true
    echo "Regenerate it with the same scenario: sdd status > dashboard.txt" >&2
    exit 1
  fi
fi

echo "Ephemeral project created in $(basename "$WORK") (self-cleaned)."
echo "Run the same loop in your own folder with the same commands."
