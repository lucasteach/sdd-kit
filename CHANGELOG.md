# Changelog

All notable changes to `sdd` (SDD-Kit CLI) are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) · Versioning: [SemVer](https://semver.org/) (decision D5).

## [1.0.8] - 2026-09-21
### Added
- Couche d'accueil publique : `CHANGELOG.md`, `SECURITY.md` (divulgation privée via GitHub, accusé ≤ 48 h, divulgation coordonnée), `CONTRIBUTING.md` (issues bienvenues, PRs externes non acceptées pour l'instant).
- `examples/hello-sdd/` : premier projet SDD en 2 minutes. `demo.sh` **auto-vérifie** que `dashboard.txt` correspond à la sortie réelle de `sdd status`, et la CI exécute ce script — le dashboard documenté ne peut donc pas mentir.
- Suite de tests portée à **231 assertions** (T39 : l'exemple est rejoué et son dashboard vérifié à chaque exécution de la suite).
### Changed
- Distribution **publique, libre et sans authentification** (MIT) : le `.nupkg` est téléchargeable via GitHub Releases. GitHub Packages est écarté comme canal public (un PAT y est requis même pour un paquet public) ; nuget.org reste l'objectif pour l'installation en une commande.
- Identité publique unifiée : auteur des commits, LICENSE et métadonnées du paquet → `lucasteach`.
### Removed
- Journal de développement interne (`AGENT_STATE.md`, `BACKLOG.md`) retiré du dépôt et de tout l'historique — les templates de la CLI continuent de les générer pour les projets qu'elle initialise.
### Fixed
- Le dépôt ne déclenche plus son propre avertissement SDD-L008 (`**Responsable** : Owner` → libellé français).

## [1.0.7] - 2026-09-21
### Fixed
- Le paquet publié n'embarque plus les chemins de build locaux (PDB et Source Link retirés : `DebugType=none`).

## [1.0.6] - 2026-09-21
### Changed
- Anonymisation complète de l'historique : références nominatives du projet-source remplacées par « projet-source », prompts shell génériques (`dev@poste`).

## [1.0.5] - 2026-09-21
### Added
- README bilingue EN/FR.
### Fixed
- Attribution des commits vers le noreply d'identité (`ID+user@users.noreply.github.com`) — protège contre l'accaparement des alias legacy.

## [1.0.4] - 2026-09-18
### Added
- `sdd --version` / `-v`.
- Validation d'arguments : `status foo` et flags inconnus → erreur + exit ≠ 0 ; `<cmd> --help` → usage, exit 0.
- Chaînes multilignes (`"""` / `'''`) dans `sdd.toml`.
### Changed
- Aide honnête : les 8 commandes implémentées, aucune promesse de phase future.
- `sdd adopt` sans constat : message explicite au lieu d'un rang vide `(BUK-001…BUK-000)`.
- DRY : `PhaseMentions` unique dans `SpecModel` (partagée trace/brief).
- Test de version à source unique (assembly) + une assertion anti-dérive.
### Fixed
- Liste noire i18n portée à 6 termes ; corrections mineures DeepSeek (BUK-016…022).

## [1.0.3] - 2026-09-16
### Fixed
- Anglicisme résiduel dans les chaînes FR → « responsable » (conformité D3).

## [1.0.2] - 2026-09-16
### Fixed
- `sdd decide` idempotent : texte identique déjà ratifié → no-op (zéro doublon dans Historique, zéro commit) ; `--force` pour rééditer.

## [1.0.1] - 2026-09-16
### Added
- Règle SDD-L008 : termes non-FR dans `docs/` → AVERTISSEMENT « langue non conforme au pin D3 » (liste noire unique `Lint.LangBlacklist`).
### Fixed
- Spanglish fondateur des templates et ressources embarquées → français (D3).

## [1.0.0] - 2026-09-16
### Added
- **P1** : `sdd init` (doctrine v1.0 embarquée verbatim) + `sdd new` (template canonique GWT, placeholders `[À RATIFIER]`, zéro prose inventée).
- **P2** : `sdd lint` (règles SDD-L001..L007 + registre de waivers) et `sdd status` (tableau ASCII normatif Annexe A, testé byte-identique).
- **P3** : `sdd decide` (ratification + Historique + commit atomique), `sdd trace` (convention commit-msg), `sdd agent-brief` (prompt auto-suffisant pour agent borné).
- **P4** : `sdd adopt` (audit brownfield : routes orphelines, mocks, liens morts HTTP, fausses confirmations, métriques contradictoires → entrées BUK), CI gate `lint --ci` (sortie condensée + annotations GitHub Actions + exit code bloquant) et étape de tests dans le gate.
- Distribution : dotnet tool (voir la section Install du README) ; licence MIT (D7) ; runner d'assertions intégré sans dépendance NuGet.
