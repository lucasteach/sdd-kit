# sdd — SDD-Kit CLI

**Spec-Driven Development, made executable.** A doctrine pack + CLI that turns the SDD methodology into infrastructure: canonical templates, a rules referee, repo memory, bounded-agent briefs, and a brownfield on-ramp. Every project that follows the tool gets SDD for free; every project that drifts knows it immediately.

Spec: `docs/specs/SPEC-OUTIL-SDD.md` (v1.1, FR) · License MIT · .NET 10 dotnet tool · **zero NuGet dependencies** (BCL only).

## Why

The methodology was built empirically over 50+ agent↔owner sessions: spec before code, dated and statused decisions, atomic commits, an anti-forgetting backlog, a repo memory file, bounded tasks, and an honesty layer (no fake confirmations). The doctrine should not live in one architect's head or in chat logs — it lives in the repo, is enforced in CI, and transfers without apprenticeship.

## Commands

| Command | What it does |
|---|---|
| `sdd init --projet <name>` | Greenfield: scaffolds `docs/DOCTRINE.md` (embedded verbatim), `AGENT_STATE.md`, `BACKLOG.md`, `docs/specs/`, CI gate workflow, `sdd.toml` |
| `sdd adopt --projet <name>` | Brownfield on-ramp: audits existing code (orphan routes, mocks/placeholders, dead links, fake success toasts, contradictory metrics) → each finding becomes an open `BUK-*` backlog entry with file:line origin |
| `sdd new <FAMILY>` | Creates `docs/specs/SPEC-<FAMILY>.md` from the canonical template — all sections, GWT skeleton, `[À RATIFIER]` placeholders, zero invented prose |
| `sdd lint [--ci]` | The referee: SDD-L001..L008 + waiver audit (SDD-WVR); errors block CI, warnings don't; `--ci` = condensed output + GitHub annotations |
| `sdd status` | Normative ASCII dashboard (counters, in-flight spec, next milestone, alerts) |
| `sdd decide <SPEC> <Dn> "<text>" --ratifiee [--force]` | Ratifies a decision: updates the line, bumps minor version, appends to Historique, creates one atomic git commit. Idempotent: same text → no-op |
| `sdd trace <REQ>` | Which commits mention the REQ (commit-msg convention), linked phases, bounded tasks, global status |
| `sdd agent-brief <SPEC> <Pn> --pour <agent>` | Generates a self-sufficient prompt for a bounded agent: strict scope, ratified decisions embedded, hard rules, commit/report formats |

The CLI never writes spec prose. Skeleton, referee, trace — decisions stay human.

## Install

```bash
# from a local build
dotnet pack src/Sdd.Cli -c Release -o nupkg
dotnet tool install -g sdd --add-source ./nupkg

# from GitHub Packages (authenticated feed)
dotnet nuget add source ghpackages \
  --source-url https://nuget.pkg.github.com/lucasteach/index.json \
  --username <your-gh-user> --password <classic-PAT-with-write:packages> --store-password-in-clear-text
dotnet tool install -g sdd --source ghpackages
```

Requires .NET SDK 10 (TFM `net10.0`).

## Development

```bash
dotnet build tests/Sdd.Tests
dotnet run --project tests/Sdd.Tests   # 229 assertions, exit ≠ 0 on failure
```

Zero NuGet dependencies: BCL only + an integrated assertion runner (no xunit on purpose — justification in commit history).

The repo dogfoods itself: GitHub Actions runs the test suite and `sdd lint --ci` on every push; the CI gate is the same workflow `sdd init` generates.

---

# sdd — SDD-Kit CLI (documentation en français)

Outil de la méthodologie **Spec-Driven Development** (spec `docs/specs/SPEC-OUTIL-SDD.md`, v1.1 ; P1–P4 approuvées). Frontière dure : la CLI fournit **squelette, arbitre et trace** ; elle n'écrit **jamais** de prose de spec. Les décisions restent propriété de l'humain.

## État d'implémentation

| Commande | Phase | Statut |
|---|---|---|
| `sdd init` | P1 | implémenté |
| `sdd new` | P1 | implémenté |
| `sdd lint` | P2 | implémenté (SDD-L001-L008 + waivers + SDD-WVR) |
| `sdd status` | P2 | implémenté (format normatif Annexe A) |
| `sdd decide` | P3 | implémenté (commit atomique via git, idempotent) |
| `sdd trace` | P3 | implémenté (convention commit-msg) |
| `sdd agent-brief` | P3 | implémenté (prompt auto-suffisant) |
| `sdd adopt` | P4 | implémenté (audit brownfield → BUK-* au BACKLOG, append honnête) |
| CI gate | P4 | workflow `sdd-lint.yml` fonctionnel (tests + `lint --ci`, bloquant sur erreur) |

## Usage

### `sdd init --projet <nom>`

Dans le répertoire courant, crée :

- `docs/DOCTRINE.md` — doctrine v1.0 embarquée **verbatim** par la CLI (pin)
- `docs/AGENT_STATE.md` — structure vide + règle de reprise (« Lis docs/AGENT_STATE.md et continue »)
- `docs/BACKLOG.md` — en-tête + règle anti-oubli littérale
- `docs/specs/` — répertoire vide
- `.github/workflows/sdd-lint.yml` — CI gate : tests + `sdd lint --ci`
- `sdd.toml` — nom du projet + pin doctrine v1.0

`init` **n'écrase jamais** des fichiers existants et ne fait aucun commit : le commit initial (artefacts + tree propre) relève du contrat humain (doctrine, règle 4).

### `sdd adopt --projet <nom>`

Onramp brownfield : audit guidé du projet existant (routes orphelines, mocks/placeholders, liens morts vérifiés par HTTP, fausses confirmations, métriques contradictoires). Chaque constat devient une entrée `BUK-*` au BACKLOG avec origine fichier:ligne et statut ouvert ; si `docs/BACKLOG.md` préexiste, les entrées sont appenduées sous un marqueur daté — jamais écrasées, jamais abandonnées en silence. Les infrastructures SDD sont créées autour ; le code et l'historique du projet restent intacts.

### `sdd new <FAMILLE>`

Crée `docs/specs/SPEC-<FAMILLE>.md` depuis le template canonique embarqué : toutes les sections (Propos, Portée, Exigences fonctionnelles en GWT, Phases, Décisions, Impacts, Notes croisées, Limitations, Historique v1.0), chaque texte attendu marqué `[À RATIFIER]`. Consigne le Brouillon dans « Spécifications en vol » de `docs/AGENT_STATE.md` (REQ-CLI03). N'écrase jamais une spec existante.

### Les autres commandes

`sdd lint [--ci]` — arbitre SDD-L001..L008 + audit des waivers (SDD-WVR), sortie normative Annexe A ou condensée `--ci` (`RESUME` machine-readable + annotations GitHub). `sdd status` — tableau ASCII normatif. `sdd decide` — ratification avec bump Historique et commit atomique, idempotente (`--force` pour rééditer). `sdd trace` — commits mentionnant une REQ (convention commit-msg), phases liées, statut global. `sdd agent-brief` — prompt auto-suffisant pour agent borné.

## Cycle de release

`dotnet pack -c Release -o nupkg` → `dotnet nuget push nupkg/sdd.<ver>.nupkg --source https://nuget.pkg.github.com/lucasteach/index.json --api-key <PAT-classique-write:packages>`. Semver classique (D5) ; licence MIT (D7) ; feed interne d'organisation (D6).

## Développement

```bash
dotnet build tests/Sdd.Tests
dotnet run --project tests/Sdd.Tests   # 229 assertions, exit ≠ 0 si échec
```

Zéro dépendance NuGet : BCL seul + runner d'assertions intégré (justification dans l'historique). Le repo dogfoode son propre workflow CI (tests + lint sur chaque push/PR).
