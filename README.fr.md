# sdd — SDD-Kit CLI

**Le Spec-Driven Development, rendu exécutable.** Un pack de doctrine + une CLI qui transforme la méthodologie SDD en infrastructure : templates canoniques, arbitre de règles, mémoire du repo, briefs d'agents bornés et onramp brownfield. Tout projet qui suit l'outil obtient le SDD gratuitement ; tout projet qui s'en écarte le sait immédiatement.

Spec : `docs/specs/SPEC-OUTIL-SDD.md` (v1.1, FR) · Licence MIT · outil `dotnet` .NET 10 · **zéro dépendance NuGet** (BCL seul).

> **English** — this README is also available in full English: [`README.md`](README.md).

## Pourquoi

La méthodologie a été construite empiriquement sur plus de 50 sessions agent↔responsable : la spec avant le code, des décisions datées et statutées, des commits atomiques, un backlog anti-oubli, un fichier de mémoire du repo, des tâches bornées et une couche d'honnêteté (aucune fausse confirmation). La doctrine ne doit pas vivre dans la tête d'un architecte ni dans des journaux de chat — elle vit dans le repo, se défend en CI et se transmet sans apprentissage.

## Langue (v1.1)

Les artefacts sont générés dans **la langue du projet**, pas celle de la CLI :

- `sdd init --projet <nom> --lang fr|en` — `fr` ou `en` ; **défaut `en`**.
- `sdd adopt --projet <nom> --lang fr|en` — idem, persisté dans `sdd.toml`.
- `sdd.toml` porte `[projet] lang = "fr" | "en"` ; `sdd new` le lit et génère le template correspondant.

Les deux doctrines (`DOCTRINE.fr.md`, `DOCTRINE.en.md`) embarquent les mêmes 14 règles — les quatre règles dures #11–#14 ne sont pas une traduction différée. L'arbitre est bilingue : une spec écrite en anglais n'échoue jamais sur des motifs français, et inversement. Les messages de la CLI et le dashboard ASCII restent en français ; leur localisation relève de la v1.2.

## Commandes

| Commande | Ce qu'elle fait |
|---|---|
| `sdd init --projet <nom> [--lang fr\|en]` | Greenfield : crée `docs/DOCTRINE.md` (verbatim embarqué, langue du projet), `AGENT_STATE.md`, `BACKLOG.md`, `docs/specs/`, le workflow CI et `sdd.toml` |
| `sdd adopt --projet <nom> [--lang fr\|en]` | Onramp brownfield : audite le code existant (routes orphelines, mocks/placeholders, liens morts, fausses confirmations, métriques contradictoires) → chaque constat devient une entrée `BUK-*` ouverte avec origine fichier:ligne |
| `sdd new <FAMILLE>` | Crée `docs/specs/SPEC-<FAMILLE>.md` depuis le template canonique de la langue du projet — toutes les sections, squelette GWT, placeholders, zéro prose inventée |
| `sdd lint [--ci]` | L'arbitre : SDD-L001..L008 + audit des waivers (SDD-WVR) ; les erreurs bloquent la CI, pas les warnings ; `--ci` = sortie condensée + annotations GitHub |
| `sdd status` | Tableau de bord ASCII normatif (compteurs, spec en vol, prochain jalon, alertes) |
| `sdd decide <SPEC> <Dn> "<texte>" --ratifiee [--force]` | Ratifie une décision : met à jour la ligne, bumpe la version mineure, ajoute à l'Historique et crée un commit git atomique. Idempotent : texte identique → no-op |
| `sdd trace <REQ>` | Quels commits mentionnent la REQ (convention commit-msg), phases liées, tâches bornées, statut global |
| `sdd agent-brief <SPEC> <Pn> --pour <agent>` | Génère un prompt auto-suffisant pour un agent borné : périmètre strict, décisions ratifiées incrustées, règles dures, formats de commit/report |

La CLI n'écrit jamais de prose de spec. Squelette, arbitre, trace — les décisions restent humaines.

## Essayer

`examples/hello-sdd/` crée un projet éphémère en deux minutes, affiche le
dashboard et vérifie que sa propre sortie documentée est toujours la vraie :

```bash
bash examples/hello-sdd/demo.sh        # défaut : en
bash examples/hello-sdd/demo.sh fr     # même boucle, artefacts en français
```

## Installation

Téléchargement public, sans compte, sans authentification : récupérez `sdd.<version>.nupkg` depuis les [releases GitHub](https://github.com/lucasteach/sdd-kit/releases) et installez-le depuis le dossier qui contient le fichier :

```bash
dotnet tool install -g sdd --add-source /chemin/vers/le/dossier/du/nupkg
```

Depuis les sources :

```bash
dotnet pack src/Sdd.Cli -c Release -o nupkg
dotnet tool install -g sdd --add-source ./nupkg
```

> `dotnet tool install -g sdd` sans `--add-source` (NuGet.org) est le canal visé pour l'installation en une commande ; il sera documenté ici une fois publié. GitHub Packages n'est **pas** un canal de téléchargement libre — un PAT y est requis même pour un paquet public.

Nécessite le SDK .NET 10 (TFM `net10.0`). Zéro dépendance NuGet, aucune télémétrie.

## Développement

```bash
dotnet build tests/Sdd.Tests
dotnet run --project tests/Sdd.Tests   # 280 assertions, exit ≠ 0 si échec
```

Zéro dépendance NuGet : BCL seul + runner d'assertions intégré (pas de xunit à dessein — justification dans l'historique des commits).

Le repo dogfoode son propre workflow : GitHub Actions exécute la suite de tests (qui rejoue l'exemple `hello-sdd`) et `sdd lint --ci` à chaque push ; le gate CI est le même workflow que celui généré par `sdd init`.
