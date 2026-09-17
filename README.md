# sdd — SDD-Kit CLI

Outil de la méthodologie **Spec-Driven Development** (spec `docs/specs/SPEC-OUTIL-SDD.md`, v1.1 ; P1–P4 approuvées 16/09).

Frontière dure : la CLI fournit **squelette, arbitre et trace** ; elle n'écrit
**jamais** de prose de spec. Les décisions restent propriété de l'humain.

## État d'implémentation

| Commande | Phase | Statut |
|---|---|---|
| `sdd init` | P1 | implémenté |
| `sdd new` | P1 | implémenté |
| `sdd lint` | P2 | implémenté (L001-L007 + waivers) |
| `sdd status` | P2 | implémenté (format normatif Annexe A) |
| `sdd decide` | P3 | implémenté (commit atomique via git) |
| `sdd trace` | P3 | implémenté (convention commit-msg) |
| `sdd agent-brief` | P3 | implémenté (prompt auto-suffisant) |
| `sdd adopt` | P4 | implémenté (audit brownfield → BUK-* au BACKLOG) |
| CI gate | P4 | workflow `sdd-lint.yml` fonctionnel (`lint --ci`, bloquant sur erreur) |

## Installation

Prérequis : .NET SDK (net10.0 ; `net8.0+` accepté).

```bash
# depuis la racine du repo
dotnet pack src/Sdd.Cli -c Release -o nupkg
dotnet tool install -g sdd --add-source ./nupkg
sdd --help
```

(Sans installation globale : `dotnet run --project src/Sdd.Cli -- <args>`.)

## Usage

### `sdd init --projet <nom>`

Dans le répertoire courant, crée :

- `docs/DOCTRINE.md` — doctrine v1.0 embarquée **verbatim** par la CLI (pin)
- `docs/AGENT_STATE.md` — structure vide + règle de reprise (« Lee docs/AGENT_STATE.md y continúa »)
- `docs/BACKLOG.md` — en-tête + règle anti-oubli littérale
- `docs/specs/` — répertoire vide
- `.github/workflows/sdd-lint.yml` — placeholder volontairement bloquant (P2)
- `sdd.toml` — nom du projet + pin doctrine v1.0

`init` **n'écrase jamais** des fichiers existants et ne fait aucun commit :
le commit initial (artefacts + tree propre) relève du contrat humain (doctrine,
règle 4).

### `sdd new <FAMILLE>`

Crée `docs/specs/SPEC-<FAMILLE>.md` depuis le template canonique embarqué :
toutes les sections (Propos, Portée, Exigences fonctionnelles en GWT, Phases,
Décisions, Impacts, Notes croisées, Limitations, Historique v1.0), chaque texte
attendu marqué `[À RATIFIER]`. Consigne aussi le Brouillon dans la section
« Spécifications en vol » de `docs/AGENT_STATE.md` (REQ-CLI03). N'écrase jamais
une spec existante.

```bash
mkdir mon-projet && cd mon-projet
sdd init --projet mon-projet
git init -b main && git add -A && git commit -m "chore(sdd): init — artefacts doctrine v1.0"
sdd new PORTAIL-CITOYEN
```

## Développement

```bash
dotnet build tests/Sdd.Tests
dotnet run --project tests/Sdd.Tests   # 49 assertions, exit ≠ 0 si échec
```

Zéro dépendance NuGet : BCL seul + runner d'assertions intégré (voir report P1
pour la justification).
