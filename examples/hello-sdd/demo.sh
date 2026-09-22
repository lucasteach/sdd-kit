#!/usr/bin/env bash
# hello-sdd — generate a minimal SDD project in the project's language, show the
# dashboard, then CHECK that dashboard.<lang>.txt is still the tool's REAL output.
# Requires .NET SDK 10. Runnable from anywhere.
#   ./demo.sh        -> English artifacts (default)
#   ./demo.sh fr     -> French artifacts
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

LANG_CODE="${1:-en}"
case "$LANG_CODE" in
  fr|en) ;;
  *) echo "usage: $0 [fr|en]   (défaut : en)" >&2; exit 2 ;;
esac

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build "$REPO/src/Sdd.Cli" -v q --nologo >/dev/null

# CLI resolved from the repo — no global install required
sdd_cli() { dotnet run --project "$REPO/src/Sdd.Cli" --no-build -- "$@"; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# `sdd init` writes in the CURRENT directory: create the project folder first.
mkdir -p "$WORK/hello-sdd"
cd "$WORK/hello-sdd"

sdd_cli init --projet hello-sdd --lang "$LANG_CODE"
sdd_cli new HELLO

# mark the spec as in flight (same as editing AGENT_STATE + Status by hand)
edit_in_place() {
  sed -i.bak "$1" "$2"
  rm -f "$2.bak"
}

if [ "$LANG_CODE" = "fr" ]; then
  edit_in_place 's/\*\*Statut\*\* : Brouillon/**Statut** : En phase/' docs/specs/SPEC-HELLO.md
  edit_in_place 's/| Phase active | — |/| Phase active | P1 |/' docs/AGENT_STATE.md
  edit_in_place 's/| Spec de référence | — |/| Spec de référence | SPEC-HELLO |/' docs/AGENT_STATE.md
  EXPECTED="$HERE/dashboard.fr.txt"
else
  edit_in_place 's/\*\*Status\*\*: Draft/**Status**: In flight/' docs/specs/SPEC-HELLO.md
  edit_in_place 's/| Active phase | — |/| Active phase | P1 |/' docs/AGENT_STATE.md
  edit_in_place 's/| Reference spec | — |/| Reference spec | SPEC-HELLO |/' docs/AGENT_STATE.md
  EXPECTED="$HERE/dashboard.en.txt"
fi

echo
sdd_cli status
echo
sdd_cli lint
echo

# The documented dashboard must not be able to lie: it is produced by the tool,
# never written by hand. The test suite runs this script (and CI runs the suite),
# so any drift fails the gate.
sdd_cli status > "$WORK/status.reel.txt"
if [ -f "$EXPECTED" ]; then
  if diff -u "$EXPECTED" "$WORK/status.reel.txt" >/dev/null; then
    echo "OK — $(basename "$EXPECTED") matches the real output of sdd status ($LANG_CODE)."
  else
    echo "FAIL — $(basename "$EXPECTED") no longer matches the real output ($LANG_CODE):" >&2
    diff -u "$EXPECTED" "$WORK/status.reel.txt" >&2 || true
    exit 1
  fi
fi

echo "Ephemeral project created in $(basename "$WORK") (self-cleaned), language=$LANG_CODE."
