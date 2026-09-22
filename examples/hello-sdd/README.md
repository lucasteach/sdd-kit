# hello-sdd — your first SDD project in 2 minutes

```bash
./demo.sh        # English artifacts (default)
./demo.sh fr     # French artifacts
```

The script scaffolds a throwaway project in the language you pass, prints the
dashboard and the referee output, then verifies that `dashboard.<lang>.txt`
matches the tool's **real** output. The test suite runs the same script, so the
documented dashboard cannot drift.

Or by hand (requires .NET SDK 10; `sdd` installed, or run it from the repo):

```bash
mkdir hello-sdd && cd hello-sdd   # `sdd init` writes in the CURRENT directory
sdd init --projet hello-sdd --lang en   # or --lang fr; default is en
sdd new HELLO                     # scaffolds docs/specs/SPEC-HELLO.md in that language
sdd status                        # the normative ASCII dashboard
sdd lint                          # the referee: SDD-L001..L008 (+ SDD-WVR)
```

What you get — real output, identical to `examples/hello-sdd/dashboard.en.txt`:

```
┌──────────────────────────────────────────────────────────────┐
│  SDD-Kit · hello-sdd                  doctrine v1.1 (pin)    │
├──────────────────────────────────────────────────────────────┤
│  SPECS            1   Brouillon 0 · Approuvée 0 · En phase 1 │
│  DÉCISIONS        1   ratifiée 0 · différée 0 · ouverte 1    │
│  BACKLOG          0   ouvert 0 · clos 0                      │
│  TÂCHES BORNÉES   0   vertes 0 · rouges 0                    │
├──────────────────────────────────────────────────────────────┤
│  EN VOL   SPEC-HELLO · P1 ([TO RATIFY])             ◐ 0 %    │
│  JALON    P1 — [TO RATIFY]                                   │
└──────────────────────────────────────────────────────────────┘
```

The dashboard labels stay French (localizing them is v1.2 work); only the
artifacts follow the project language — with `--lang fr` the same run shows
`[À RATIFIER]` instead of `[TO RATIFY]`.

Progress is **computed**: one phase, none ratified yet → `0 %`. The CLI never
prints a number it cannot derive from the repo.

Next steps: fill the placeholders **by hand** (the CLI never writes spec prose),
ratify a decision with `sdd decide SPEC-HELLO D1 "…" --ratifiee` (it commits
atomically), and wire the generated `.github/workflows/sdd-lint.yml` into your
CI. That's the whole loop: doctrine → spec → bounded work → referee.

MIT — free to copy, fork and reuse. No account, no telemetry, no network call
except the optional dead-link check in `sdd adopt` (skippable with `SDD_SKIP_NETWORK=1`).

---

# hello-sdd — votre premier projet SDD en 2 minutes

```bash
./demo.sh        # artefacts en anglais (défaut)
./demo.sh fr     # artefacts en français
```

Le script crée un projet éphémère dans la langue demandée, affiche le dashboard
et la sortie de l'arbitre, puis vérifie que `dashboard.<lang>.txt` correspond à
la sortie **réelle** de l'outil. La suite de tests exécute le même script : le
dashboard documenté ne peut donc pas mentir.

À la main (SDK .NET 10 requis ; `sdd` installé, ou lancé depuis le repo) :

```bash
mkdir hello-sdd && cd hello-sdd   # `sdd init` écrit dans le répertoire COURANT
sdd init --projet hello-sdd --lang fr   # ou --lang en ; défaut : en
sdd new HELLO                     # crée docs/specs/SPEC-HELLO.md dans cette langue
sdd status                        # le dashboard ASCII normatif
sdd lint                          # l'arbitre : SDD-L001..L008 (+ SDD-WVR)
```

Le dashboard ci-dessus est la sortie réelle ; en `--lang fr`, la cellule de
phase affiche `[À RATIFIER]` au lieu de `[TO RATIFY]` (voir
`examples/hello-sdd/dashboard.fr.txt`).

La progression est **calculée** : une phase, aucune ratifiée → `0 %`. La CLI
n'affiche jamais un nombre qu'elle ne peut pas dériver du repo.

Étapes suivantes : remplir les placeholders **à la main** (la CLI n'écrit jamais
de prose de spec), ratifier une décision avec
`sdd decide SPEC-HELLO D1 "…" --ratifiee` (commit atomique), puis brancher le
workflow `.github/workflows/sdd-lint.yml` généré dans votre CI. C'est toute la
boucle : doctrine → spec → travail borné → arbitre.

MIT — libre de copie, de fork et de réutilisation. Aucun compte, aucune
télémétrie, aucun appel réseau sauf la vérification optionnelle des liens morts
dans `sdd adopt` (désactivable par `SDD_SKIP_NETWORK=1`).
