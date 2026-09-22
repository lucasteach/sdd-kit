# sdd — SDD-Kit CLI

**Spec-Driven Development, made executable.** A doctrine pack + CLI that turns the SDD methodology into infrastructure: canonical templates, a rules referee, repo memory, bounded-agent briefs, and a brownfield on-ramp. Every project that follows the tool gets SDD for free; every project that drifts knows it immediately.

Spec: `docs/specs/SPEC-OUTIL-SDD.md` (v1.1, FR) · License MIT · .NET 10 dotnet tool · **zero NuGet dependencies** (BCL only).

> **Français** — ce README existe aussi en version française complète : [`README.fr.md`](README.fr.md).

## Why

The methodology was built empirically over 50+ agent↔owner sessions: spec before code, dated and statused decisions, atomic commits, an anti-forgetting backlog, a repo memory file, bounded tasks, and an honesty layer (no fake confirmations). The doctrine should not live in one architect's head or in chat logs — it lives in the repo, is enforced in CI, and transfers without apprenticeship.

## Language (v1.1)

Artifacts are generated in **the project's language**, not the CLI's:

- `sdd init --projet <name> --lang fr|en` — `fr` or `en`; **default `en`**.
- `sdd adopt --projet <name> --lang fr|en` — same, persisted in `sdd.toml`.
- `sdd.toml` carries `[projet] lang = "fr" | "en"`; `sdd new` reads it and scaffolds the matching template.

Both doctrines (`DOCTRINE.fr.md`, `DOCTRINE.en.md`) embed the same 14 rules — the four hard rules #11–#14 are not a deferred translation. The referee is bilingual: a spec written in English never fails on French patterns, and vice versa. CLI messages and the ASCII dashboard remain French; localizing them is v1.2 work.

## Commands

| Command | What it does |
|---|---|
| `sdd init --projet <name> [--lang fr\|en]` | Greenfield: scaffolds `docs/DOCTRINE.md` (embedded verbatim, project language), `AGENT_STATE.md`, `BACKLOG.md`, `docs/specs/`, CI gate workflow, `sdd.toml` |
| `sdd adopt --projet <name> [--lang fr\|en]` | Brownfield on-ramp: audits existing code (orphan routes, mocks/placeholders, dead links, fake success toasts, contradictory metrics) → each finding becomes an open `BUK-*` backlog entry with file:line origin |
| `sdd new <FAMILY>` | Creates `docs/specs/SPEC-<FAMILY>.md` from the canonical template of the project language — all sections, GWT skeleton, placeholders, zero invented prose |
| `sdd lint [--ci]` | The referee: SDD-L001..L008 + waiver audit (SDD-WVR); errors block CI, warnings don't; `--ci` = condensed output + GitHub annotations |
| `sdd status` | Normative ASCII dashboard (counters, in-flight spec, next milestone, alerts) |
| `sdd decide <SPEC> <Dn> "<text>" --ratifiee [--force]` | Ratifies a decision: updates the line, bumps minor version, appends to the history, creates one atomic git commit. Idempotent: same text → no-op |
| `sdd trace <REQ>` | Which commits mention the REQ (commit-msg convention), linked phases, bounded tasks, global status |
| `sdd agent-brief <SPEC> <Pn> --pour <agent>` | Generates a self-sufficient prompt for a bounded agent: strict scope, ratified decisions embedded, hard rules, commit/report formats |

The CLI never writes spec prose. Skeleton, referee, trace — decisions stay human.

## Try it

`examples/hello-sdd/` scaffolds a throwaway project in two minutes, prints the
dashboard, and checks that its own documented output is still the real one:

```bash
bash examples/hello-sdd/demo.sh        # default: en
bash examples/hello-sdd/demo.sh fr     # same loop, French artifacts
```

## Install

Public download, no account, no authentication: grab `sdd.<version>.nupkg` from
[GitHub Releases](https://github.com/lucasteach/sdd-kit/releases) and install it
from the folder that contains the file:

```bash
dotnet tool install -g sdd --add-source /path/to/folder/with/the/nupkg
```

From source:

```bash
dotnet pack src/Sdd.Cli -c Release -o nupkg
dotnet tool install -g sdd --add-source ./nupkg
```

> `dotnet tool install -g sdd` without `--add-source` (NuGet.org) is the intended
> one-command channel; it will be documented here once published. GitHub Packages
> is **not** a free-download channel — it requires a PAT even for public packages.

Requires .NET SDK 10 (TFM `net10.0`). Zero NuGet dependencies, no telemetry.

## Development

```bash
dotnet build tests/Sdd.Tests
dotnet run --project tests/Sdd.Tests   # 280 assertions, exit ≠ 0 on failure
```

Zero NuGet dependencies: BCL only + an integrated assertion runner (no xunit on purpose — justification in commit history).

The repo dogfoods itself: GitHub Actions runs the test suite (which replays the `hello-sdd` example) and `sdd lint --ci` on every push; the CI gate is the same workflow `sdd init` generates.
