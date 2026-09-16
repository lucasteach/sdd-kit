# Spécification : Outil SDD (méthodologie + CLI)
**Version** : 1.1 (Brouillon — P1 approuvée par l'owner 2026-09-16)
**Statut** : Brouillon
**Créée** : 2026-09-16
**Responsable** : lucasteach

## Historique
- v1.0 (16/09/2026) : Brouillon initial
- v1.1 (16/09/2026) : D1-D7 ratifiées par l'owner

## Propos

Fournir aux équipes de tout projet un **kit doctrine + CLI** qui transforme la méthodologie
SDD construite sur un projet-source en infrastructure exécutable : templates
canoniques, arbitre de règles, machine à états, générateur de briefs
agent bornés, et onramp pour projets existants.

La doctrine ne doit plus vivre dans la tête d'un architecte ni dans
les chats d'un agent : elle vit dans le repo, se défend en CI, et
se transmet sans apprentissage. **Tout projet qui respecte l'outil
obtient le SDD gratuit ; tout projet qui s'en écarte le sait
immédiatement.**

Deux publics :

- **Greenfield** (projets IA nouveaux) : le projet naît avec
  DOCTRINE.md, AGENT_STATE, backlog, templates, et contrat
  humain↔agent dès le commit zéro.
- **Brownfield** (améliorations à projets existants) : onramp guidé
  qui audite le projet, sème le backlog de ses invisibles, et
  produit les premiers Brouillons à partir des hallazgos.

Spécification **docs-only** : aucun code avant approbation owner.

## Portée

- **Inclus** : doctrine pack (DOCTRINE.md + templates canoniques) ;
  CLI (init, new, lint, status, decide, trace, agent-brief, adopt) ;
  CI gate ; contrat humain↔agent généré ; annexes visuelles (ASCII
  comme spec UI ; mockup dashboard comme vision v3 non normative).
- **Exclus** : GUI v1 (maintien prématuré pour un outil de docs) ;
  génération automatique de prose de spec (Propos, Portée,
  décisions owner — domaine humain) ; intégration spécifique à
  projet-source (l'outil est agnostique ; projet-source est le cas d'étude).
- **Frontière dure** : la CLI n'écrit jamais de prose de spec ;
  elle fournit squelette, arbitre et trace. Les décisions restent
  propriété de l'humain.

## Contexte

La méthodologie SDD a été construite empiriquement sur un projet-source en
50+ sessions agent↔owner :

- Spec avant code, Brouillon → ratification → tâches bornées
- Commits propres avec hash et report
- Décisions avec statut (ouverte / ratifiée / différée)
- AGENT_STATE comme mémoire du repo
- Backlog comme anti-oubli (BUX-*)
- Validation visuelle owner comme test final
- Couche d'honnêteté : zéro fausse confirmation

Cette doctrine n'est documentée nulle part de façon portable. Sans
outil, chaque nouveau projet la redécouvre — ou pire, la réinvente
mal. SDD-Kit la cristallise.

## Exigences fonctionnelles

### REQ-CLI01 : Initialisation de projet (init)
**Étant donné** un répertoire vide ou un projet sans SDD
**Quand** l'owner exécute `sdd init --projet <nom>`
**Alors** l'outil crée :
- `docs/DOCTRINE.md` (version pin)
- `docs/AGENT_STATE.md` (structure vide)
- `docs/BACKLOG.md` (en-tête + règle anti-oubli)
- `docs/specs/` (répertoire vide)
- `.github/workflows/sdd-lint.yml` (CI gate)
- `sdd.toml` (métadonnées projet)
**Et** le commit initial contient tous ces artefacts

### REQ-CLI02 : Adoption brownfield (adopt)
**Étant donné** un projet existant sans SDD
**Quand** l'owner exécute `sdd adopt --projet <nom>`
**Alors** l'outil lance un audit guidé :
- Scan des routes orphelines (non exposées dans navigation)
- Détection de mocks et placeholders
- Détection de liens morts (404, 500)
- Détection de fausses confirmations (succès vert avec N=0)
- Détection de métriques contradictoires (même écran)
**Et** chaque hallazgo devient une entrée BACKLOG avec origine
(commit/fichier/ligne) et statut ouvert
**Et** les infrastructures SDD (REQ-CLI01) sont créées autour

### REQ-CLI03 : Création de nouvelle spec (new)
**Étant donné** un projet SDD initialisé
**Quand** l'owner exécute `sdd new <FAMILLE> --brouillon`
**Alors** l'outil génère `docs/specs/SPEC-<FAMILLE>.md` avec :
- Toutes les sections canoniques (Propos, Portée, REQ GWT, Phases,
  Décisions ouvertes, Impacts, Notes croisées, Limitations)
- Section Historique initialisée
- Aucune prose inventée (placeholders marqués `[À RATIFIER]`)
**Et** une entrée AGENT_STATE est créée pour le Brouillon

### REQ-CLI04 : Arbitre (lint)
**Étant donné** un projet SDD
**Quand** l'owner (ou CI) exécute `sdd lint`
**Alors** l'outil vérifie :
- REQ sans Given/When/Then → erreur SDD-L001
- Décision sans statut → erreur SDD-L002
- Référence à spec inexistante (fantasma) → erreur SDD-L003
- Entrée BACKLOG sans origine ni statut → erreur SDD-L004
- Phase sans jalon visible → erreur SDD-L005
- Magic numbers dans snippets code → warning SDD-L006
- Spécifications sans Historique → warning SDD-L007
**Et** retourne : nb règles OK / nb erreurs / nb warnings / nb waivers
**Et** les règles ont trois niveaux : erreur (bloque CI), warning
(informatif), waivable (avec registre de justification)

### REQ-CLI05 : Tableau de bord (status)
**Étant donné** un projet SDD
**Quand** l'owner exécute `sdd status`
**Alors** l'outil affiche (ASCII) :
- Compteurs par statut (specs, décisions, backlog, tâches)
- Spécification en vol + phase courante + progression
- Prochain jalon
- Alertes actives (lint erreurs, décisions ouvertes anciennes)

### REQ-CLI06 : Décisions et ratifications (decide)
**Étant donné** une décision ouverte dans une spec
**Quand** l'owner exécute `sdd decide <SPEC> <Dn> "<texte>" --ratifiee`
**Alors** l'outil :
- Met à jour la ligne de décision (statut → ratifiée + date)
- Append au Historique de la spec (bump version mineure)
- Génère un commit atomique avec les deux changements

### REQ-CLI07 : Traçabilité (trace)
**Étant donné** une REQ (ex: REQ-VIZ01)
**Quand** l'owner exécute `sdd trace REQ-VIZ01`
**Alors** l'outil affiche :
- Les commits qui mentionnent la REQ (convention commit-msg)
- Les tâches bornées associées
- Les phases de spec concernées
- Le statut global (implémentée / partielle / non démarrée)

### REQ-CLI08 : Brief agent borné (agent-brief)
**Étant donné** une spec et une phase
**Quand** l'owner exécute `sdd agent-brief <SPEC> <Pn> --pour <agent>`
**Alors** l'outil génère un prompt texte contenant :
- Périmètre strict (inclus / exclus de la phase)
- Décisions owner déjà ratifiées (incrustées)
- Règles dures applicables
- Format de commit attendu
- Format de report attendu
**Et** le prompt est auto-suffisant : un agent sans contexte du
projet peut l'exécuter et produire un résultat conforme

### REQ-CLI09 : Gate CI
**Étant donné** une PR / commit sur le projet
**Quand** le pipeline GitHub Actions (ou équivalent) s'exécute
**Alors** `sdd lint` est exécuté comme étape bloquante
**Et** la PR est refusée si une règle niveau-erreur échoue
**Et** le commentaire de PR liste les erreurs avec liens vers la doc

## Architecture technique

Trois pièces :

1. **Doctrine pack** : `DOCTRINE.md` (texte méthodologie versionné),
   templates canoniques (markdown avec frontmatter YAML), règles
   (fichier `rules.yml` déclaratif). Chaque projet pin une version
   de doctrine via `sdd.toml`.
2. **CLI** : .NET console app, distribuée comme `dotnet tool`.
   Modules : init, new, lint, status, decide, trace, agent-brief,
   adopt. Parser markdown conventionné (pas libre).
3. **CI gate** : workflow YAML standard, appel à `sdd lint --ci`.

### Annexe A : Spécification d'UI de la CLI (ASCII, normative P1-P2)

Sortie attendue de `sdd status` (format exact, caractères box-drawing) :

```
lucas@dev:~/projets/mon-projet$ sdd status
┌──────────────────────────────────────────────────────────────┐
│  SDD-Kit · mon-projet                 doctrine v1.0 (pin)    │
├──────────────────────────────────────────────────────────────┤
│  SPECS            4   Brouillon 1 · Approuvée 2 · En phase 1 │
│  DÉCISIONS        9   ratifiée 6 · différée 2 · ouverte 1    │
│  BACKLOG          8   ouvert 5 · clos 3                      │
│  TÂCHES BORNÉES  12   vertes 11 · rouges 1                   │
├──────────────────────────────────────────────────────────────┤
│  EN VOL   SPEC-EXEMPLE · P1 (Composant X)          ◐ 60 %    │
│  JALON    P2 — intégration en production                     │
└──────────────────────────────────────────────────────────────┘
```

Sortie attendue de `sdd lint` (format exact) :

```
lucas@dev:~/projets/mon-projet$ sdd lint
✖ SDD-L003  référence fantasma
            AGENT_STATE.md:12 cite SPEC-INEXISTANTE.md —
            absente de docs/specs/
✖ SDD-L002  décision sans statut : D2 (SPEC-EXEMPLE)
✔ 41 règles OK · 2 erreurs · 0 waivers
```

Ces blocs ASCII sont **normatifs** : forme exacte attendue. Toute
divergence de format est une régression de l'outil.

### Annexe B : Vision tableau de bord web (v3, non normative)

Dashboard HTML statique générable depuis le repo : matrice REQ →
commits, chaleur du backlog, état global en une page, zéro
authentification. **Non normatif en v1** : aucune implémentation
avant P4, et seulement si le dogfooding démontre l'utilité.

## Phases d'implémentation

| Phase | Contenu | Jalon visible | Statut |
|-------|---------|---------------|--------|
| P1 | Doctrine pack + `sdd init` + `sdd new` (scaffold) | premier projet SDD via CLI | **approuvée 16/09** |
| P2 | `sdd lint` (règles L001-L007) + `sdd status` (ASCII) | l'outil arbitre son propre repo | **approuvée 16/09** |
| P3 | `sdd decide` + `sdd trace` + `sdd agent-brief` | contrat humain↔agent généré | à approuver |
| P4 | `sdd adopt` (brownfield) + CI gate + dogfooding complet | la doctrine se défend seule | à approuver |

## Décisions

- **D1** Nom public : repo `sdd-kit`, commande `sdd` — **RATIFIÉE 16/09**
- **D2** Distribution : `dotnet tool install -g sdd` — **RATIFIÉE 16/09**
- **D3** Langue des templates : FR par défaut, EN optionnelle v1 — **RATIFIÉE 16/09**
- **D4** Sévérité des règles : telles que REQ-CLI04 — **RATIFIÉE 16/09**
- **D5** Versionado de doctrine : semver classique — **RATIFIÉE 16/09**
- **D6** Publication : feed interne de l'organisation v1 ; nuget.org conditionnel v2 — **RATIFIÉE 16/09**
- **D7** Licence : MIT — **RATIFIÉE 16/09**

## Impacts

- **Sur projet-source** : aucun — projet-source reste le cas d'étude, pas une
  dépendance de la CLI. Migration optionnelle si l'owner le décide.
- **Sur l'équipe** : onboarding d'un nouveau projet réduit de
  plusieurs semaines à une session SDD-Kit.
- **Sur les agents** : tout agent sur projet SDD-Kit lit
  AGENT_STATE et comprend l'état sans contexte antérieur ; le brief
  généré élimine le réapprentissage à chaque session.

## Notes croisées

- projet-source est le **cas d'étude #1** : la doctrine
  est extraite de son historique de 50+ sessions.
- SPEC-CAS-VIZ-LIGNAGE contient des décisions ratifiées — exemple
  concret d'usage futur de REQ-CLI06.
- ADR-026 (tokens CSS) est un exemple de règle transposable en
  règle SDD-L* (zéro magic number).

## Limitations connues

- La CLI ne génère pas de prose de spec (Propos, Portée, etc.) —
  domaine humain.
- L'audit brownfield (REQ-CLI02) a une précision limitée aux
  patterns connus ; faux négatifs possibles sur code exotique.
- Le tableau de bord v3 est une vision, pas un engagement.
- La traçabilité REQ→commits exige une convention de commit-msg ;
  sans convention adoptée, `sdd trace` retourne vide.

