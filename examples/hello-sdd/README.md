# hello-sdd — your first SDD project in 2 minutes

```bash
./demo.sh
```

The script scaffolds a throwaway project, prints the dashboard and the referee
output, then verifies that `dashboard.txt` below still matches the tool's **real**
output. CI runs the same script, so the documented dashboard cannot drift.

Or by hand (requires .NET SDK 10; `sdd` installed, or run it from the repo):

```bash
mkdir hello-sdd && cd hello-sdd   # `sdd init` writes in the CURRENT directory
sdd init --projet hello-sdd       # or: dotnet run --path/to/sdd-kit/src/Sdd.Cli -- init --projet hello-sdd
sdd new HELLO                     # scaffolds docs/specs/SPEC-HELLO.md, all [À RATIFIER]
sdd status                        # the normative ASCII dashboard
sdd lint                          # the referee: SDD-L001..L008 (+ SDD-WVR waiver audit)
```

What you get — real output, identical to `examples/hello-sdd/dashboard.txt`:

```
┌──────────────────────────────────────────────────────────────┐
│  SDD-Kit · hello-sdd                  doctrine v1.0 (pin)    │
├──────────────────────────────────────────────────────────────┤
│  SPECS            1   Brouillon 0 · Approuvée 0 · En phase 1 │
│  DÉCISIONS        1   ratifiée 0 · différée 0 · ouverte 1    │
│  BACKLOG          0   ouvert 0 · clos 0                      │
│  TÂCHES BORNÉES   0   vertes 0 · rouges 0                    │
├──────────────────────────────────────────────────────────────┤
│  EN VOL   SPEC-HELLO · P1 ([À RATIFIER])            ◐ 0 %    │
│  JALON    P1 — [À RATIFIER]                                  │
└──────────────────────────────────────────────────────────────┘
```

`En phase 1` comes from the `**Statut** : En phase` line the demo writes into the
spec. Progress is **computed**: one phase, none ratified yet → `0 %`. The CLI never
prints a number it cannot derive from the repo.

Next steps: fill the `[À RATIFIER]` placeholders **by hand** (the CLI never
writes spec prose), ratify a decision with `sdd decide SPEC-HELLO D1 "…" --ratifiee`
(it commits atomically), and wire the generated `.github/workflows/sdd-lint.yml`
into your CI. That's the whole loop: doctrine → spec → bounded work → referee.

MIT — free to copy, fork and reuse. No account, no telemetry, no network call
except the optional dead-link check in `sdd adopt` (skippable with `SDD_SKIP_NETWORK=1`).
