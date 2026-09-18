using System.Text;
using System.Text.RegularExpressions;

namespace Sdd.Tests;

/// <summary>
/// Runner d'assertions intégré — zéro dépendance NuGet (voir report P1).
/// Exécute la CLI in-process dans des répertoires temporaires isolés.
/// Code de sortie : 0 = tout vert, 1 = au moins une assertion en échec.
/// </summary>
public static class Tests
{
    private static int _checks;
    private static int _failures;

    public static int Main()
    {
        string startup = Directory.GetCurrentDirectory();
        string sandbox = Path.Combine(Path.GetTempPath(), $"sdd-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);

        try
        {
            Test_Init_Cree_Tous_Les_Artefacts(sandbox);
            Test_Init_Necrase_Pas(sandbox);
            Test_New_Sans_Init_Echoue(sandbox);
            Test_New_Cree_Spec_Complete(sandbox);
            Test_New_Necrase_Pas_Et_Idempotence_AgentState(sandbox);
            Test_Args_Invalides(sandbox);
            Test_L001_ReqSansGwt(sandbox);
            Test_L002_DecisionSansStatut(sandbox);
            Test_L003_RefFantasma_Et_Waiver(sandbox);
            Test_L004_BacklogSansOrigine(sandbox);
            Test_L005_PhaseSansJalon(sandbox);
            Test_L006_MagicNumbers(sandbox);
            Test_L007_SansHistorique(sandbox);
            Test_Lint_Propre_Exit0(sandbox);
            Test_Status_ByteIdentique_AnnexeA(sandbox);
            Test_Dogfood_SddKit(startup);
            Test_Decide(sandbox);
            Test_Trace(sandbox);
            Test_AgentBrief(sandbox);
            Test_AgentBrief_Dogfood_P3(startup);
            Test_Adopt_Brownfield(sandbox);
            Test_Adopt_DejaInitialise(sandbox);
            Test_Lint_CI(sandbox);
            Test_Usage_Honnete();
            Test_Version();
            Test_Adopt_Vide(sandbox);
            Test_I18n_Garde(sandbox);
            Test_Decide_Idempotence(sandbox);
            Test_Adopt_BacklogPreexistant(sandbox);
            Test_Workflow_Installable(startup);
            Test_Decide_SpecIncomplete(sandbox);
            Test_Status_SansSpecEnVol(sandbox);
            Test_L003_DocsAnidados(sandbox);
            Test_WaiverInutilise(sandbox);
            Test_Workflow_AvecTests();
            Test_Args_Validees(sandbox);
            Test_TomlMultiLigne(sandbox);
            Test_PhaseMentions_Partage(sandbox);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { /* temporaire, non critique */ }
        }

        Console.WriteLine();
        Console.WriteLine($"— {_checks - _failures}/{_checks} assertions OK, {_failures} en échec —");
        return _failures == 0 ? 0 : 1;
    }

    // ---------- helpers ----------

    private static void Check(bool condition, string name)
    {
        _checks++;
        if (condition)
        {
            Console.WriteLine($"  ✔ {name}");
        }
        else
        {
            _failures++;
            Console.WriteLine($"  ✖ {name}");
        }
    }

    private static (int exit, string stdout) RunCli(string workDir, params string[] args)
    {
        string previous = Directory.GetCurrentDirectory();
        TextWriter originalOut = Console.Out;
        TextWriter originalErr = Console.Error;
        var outBuf = new StringWriter();
        Console.SetOut(outBuf);
        Console.SetError(outBuf);
        try
        {
            Directory.SetCurrentDirectory(workDir);
            int code = Program.Main(args);
            return (code, outBuf.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(previous);
        }
    }

    private static string FreshProject(string sandbox, string label)
    {
        string dir = Path.Combine(sandbox, label);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

    // ---------- tests ----------

    private static void Test_Init_Cree_Tous_Les_Artefacts(string sandbox)
    {
        Console.WriteLine("T1 — sdd init : arborescence + contenus");
        string dir = FreshProject(sandbox, "init-ok");
        var (exit, _) = RunCli(dir, "init", "--projet", "portail-citoyen");
        Check(exit == 0, "exit 0");
        Check(File.Exists(Path.Combine(dir, "docs", "DOCTRINE.md")), "docs/DOCTRINE.md");
        Check(File.Exists(Path.Combine(dir, "docs", "AGENT_STATE.md")), "docs/AGENT_STATE.md");
        Check(File.Exists(Path.Combine(dir, "docs", "BACKLOG.md")), "docs/BACKLOG.md");
        Check(Directory.Exists(Path.Combine(dir, "docs", "specs")), "docs/specs/ (vide)");
        Check(Directory.GetFiles(Path.Combine(dir, "docs", "specs")).Length == 0, "docs/specs/ ne contient aucun fichier");
        Check(File.Exists(Path.Combine(dir, ".github", "workflows", "sdd-lint.yml")), ".github/workflows/sdd-lint.yml");
        Check(File.Exists(Path.Combine(dir, "sdd.toml")), "sdd.toml");

        string doctrine = Read(Path.Combine(dir, "docs", "DOCTRINE.md"));
        Check(doctrine.StartsWith("# Doctrine SDD-Kit v1.0\n", StringComparison.Ordinal), "DOCTRINE verbatim : en-tête v1.0");
        Check(doctrine.Contains("9. **Couche d'honnêteté**", StringComparison.Ordinal)
              && doctrine.Contains("10. **Frontière outillage**", StringComparison.Ordinal), "DOCTRINE verbatim : règles 9 et 10 présentes");

        string backlog = Read(Path.Combine(dir, "docs", "BACKLOG.md"));
        Check(backlog.Contains("Toute idée reportée s'enregistre ici dans le commit qui la pose.", StringComparison.Ordinal),
              "BACKLOG : règle anti-oubli littérale");

        string state = Read(Path.Combine(dir, "docs", "AGENT_STATE.md"));
        Check(state.Contains("Lis docs/AGENT_STATE.md et continue", StringComparison.Ordinal), "AGENT_STATE : règle de reprise (FR, D3)");
        Check(state.Contains("## Spécifications en vol", StringComparison.Ordinal), "AGENT_STATE : section en vol");

        string toml = Read(Path.Combine(dir, "sdd.toml"));
        Check(toml.Contains("nom = \"portail-citoyen\"", StringComparison.Ordinal), "sdd.toml : nom du projet");
        Check(toml.Contains("version = \"1.0\"", StringComparison.Ordinal), "sdd.toml : doctrine pin v1.0");

        string wf = Read(Path.Combine(dir, ".github", "workflows", "sdd-lint.yml"));
        Check(wf.Contains("lint --ci", StringComparison.Ordinal) && wf.Contains("actions/setup-dotnet", StringComparison.Ordinal),
              "workflow CI gate fonctionnel (REQ-CLI09)");
    }

    private static void Test_Init_Necrase_Pas(string sandbox)
    {
        Console.WriteLine("T2 — sdd init : jamais d'écrasement");
        string dir = FreshProject(sandbox, "init-twice");
        Check(RunCli(dir, "init", "--projet", "p1").exit == 0, "premier init OK");
        string doctrineBefore = Read(Path.Combine(dir, "docs", "DOCTRINE.md"));
        var (exit, _) = RunCli(dir, "init", "--projet", "p1");
        Check(exit != 0, "second init refusé (exit ≠ 0)");
        Check(Read(Path.Combine(dir, "docs", "DOCTRINE.md")) == doctrineBefore, "DOCTRINE.md intact");
    }

    private static void Test_New_Sans_Init_Echoue(string sandbox)
    {
        Console.WriteLine("T3 — sdd new hors projet initialisé : refus");
        string dir = FreshProject(sandbox, "new-uninit");
        var (exit, output) = RunCli(dir, "new", "PORTAIL-CITOYEN");
        Check(exit != 0, "exit ≠ 0");
        Check(output.Contains("sdd.toml", StringComparison.Ordinal), "message pointe vers `sdd init`");
        Check(!File.Exists(Path.Combine(dir, "docs", "specs", "SPEC-PORTAIL-CITOYEN.md")), "aucun fichier créé");
    }

    private static void Test_New_Cree_Spec_Complete(string sandbox)
    {
        Console.WriteLine("T4 — sdd new PORTAIL-CITOYEN : squelette canonique");
        string dir = FreshProject(sandbox, "new-ok");
        RunCli(dir, "init", "--projet", "portail-citoyen");
        var (exit, _) = RunCli(dir, "new", "PORTAIL-CITOYEN");
        Check(exit == 0, "exit 0");

        string specPath = Path.Combine(dir, "docs", "specs", "SPEC-PORTAIL-CITOYEN.md");
        Check(File.Exists(specPath), "docs/specs/SPEC-PORTAIL-CITOYEN.md créé");
        string spec = Read(specPath);

        foreach (string section in new[]
        {
            "## Historique", "## Propos", "## Portée", "## Exigences fonctionnelles",
            "## Phases", "## Décisions", "## Impacts", "## Notes croisées", "## Limitations",
        })
        {
            Check(spec.Contains(section + "\n", StringComparison.Ordinal), $"section {section}");
        }

        Check(spec.Contains("### REQ-PORTAIL-CITOYEN01", StringComparison.Ordinal), "REQ GWT préfixée REQ-<FAMILLE>");
        Check(spec.Contains("**Étant donné**", StringComparison.Ordinal)
              && spec.Contains("**Quand**", StringComparison.Ordinal)
              && spec.Contains("**Alors**", StringComparison.Ordinal)
              && spec.Contains("**Et**", StringComparison.Ordinal), "quatuor GWT complet");
        int reqHeadings = spec.Split('\n').Count(l => l.StartsWith("### REQ-", StringComparison.Ordinal));
        Check(reqHeadings == 1, "exactement 1 cabecera ### REQ-");
        Check(spec.Contains("**Version** : 1.0 (Brouillon)", StringComparison.Ordinal), "Version 1.0 Brouillon");
        Check(spec.Contains("- v1.0 (", StringComparison.Ordinal) && spec.Contains(") : Brouillon initial", StringComparison.Ordinal),
              "Historique initialisé v1.0");
        Check(spec.Contains("[À RATIFIER]", StringComparison.Ordinal), "placeholders [À RATIFIER] présents");
        Check(!spec.Contains("{{FAMILLE}}", StringComparison.Ordinal) && !spec.Contains("{{DATE}}", StringComparison.Ordinal),
              "aucun marqueur de template non résolu");

        string state = Read(Path.Combine(dir, "docs", "AGENT_STATE.md"));
        Check(state.Contains("SPEC-PORTAIL-CITOYEN.md — Brouillon v1.0", StringComparison.Ordinal),
              "AGENT_STATE : entrée en vol créée (REQ-CLI03)");
    }

    private static void Test_New_Necrase_Pas_Et_Idempotence_AgentState(string sandbox)
    {
        Console.WriteLine("T5 — sdd new : pas d'écrasement, en vol non dupliquée");
        string dir = FreshProject(sandbox, "new-twice");
        RunCli(dir, "init", "--projet", "p2");
        Check(RunCli(dir, "new", "VIZ-LIGNAGE").exit == 0, "premier new OK");
        string specBefore = Read(Path.Combine(dir, "docs", "specs", "SPEC-VIZ-LIGNAGE.md"));
        Check(RunCli(dir, "new", "VIZ-LIGNAGE").exit != 0, "second new refusé");
        Check(Read(Path.Combine(dir, "docs", "specs", "SPEC-VIZ-LIGNAGE.md")) == specBefore, "spec intacte");
        int entries = Read(Path.Combine(dir, "docs", "AGENT_STATE.md"))
            .Split('\n').Count(l => l.StartsWith("- SPEC-VIZ-LIGNAGE.md", StringComparison.Ordinal));
        Check(entries == 1, "entrée en vol unique (pas de doublon)");
    }

    private static void Test_Args_Invalides(string sandbox)
    {
        Console.WriteLine("T6 — arguments invalides");
        string dir = FreshProject(sandbox, "bad-args");
        Check(RunCli(dir, "init").exit != 0, "init sans --projet → erreur");
        Check(RunCli(dir, "init", "--projet", "../traversee").exit != 0, "nom avec chemin → rejeté");
        Check(RunCli(dir, "new", "minuscule").exit != 0, "FAMILLE non conformes → rejetée");
        Check(RunCli(dir, "invente").exit != 0, "sous-commande inconnue → erreur");
    }

    // ---------- fixtures P2 ----------

    private static string MakeFixture(string sandbox, string label, string specContent,
        string backlog = "## Entrées\n\n- [ ] BUK-001 (2026-09-15, origine : test) truc → P1\n",
        string? tomlExtra = null)
    {
        string dir = FreshProject(sandbox, label);
        Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-T.md"), specContent);
        File.WriteAllText(Path.Combine(dir, "docs", "BACKLOG.md"),
            "# BACKLOG — t\n\n## Entrées\n\n" + (backlog.StartsWith("## Entrées") ? backlog[11..] : backlog));
        File.WriteAllText(Path.Combine(dir, "docs", "AGENT_STATE.md"),
            "# AGENT_STATE\n\n## État courant\n\n| Champ | Valeur |\n|---|---|\n| Spec de référence | — |\n");
        File.WriteAllText(Path.Combine(dir, "sdd.toml"),
            "[projet]\nnom = \"t\"\n\n[doctrine]\nversion = \"1.0\"\n" + (tomlExtra ?? ""));
        return dir;
    }

    private static (int exit, string output) RunLint(string dir)
    {
        var buf = new StringWriter();
        int code = Lint.Run(dir, buf);
        return (code, buf.ToString().Replace("\r\n", "\n"));
    }

    private static void Test_L001_ReqSansGwt(string sandbox)
    {
        Console.WriteLine("T7 — SDD-L001 : REQ sans GWT");
        string dir = MakeFixture(sandbox, "l001",
            "# Spec\n## Historique\n- v1.0 (2026-09-15) : x\n\n## Exigences\n\n### REQ-T001 : x\n**Étant donné** a\n**Quand** b\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 1, "exit 1 (erreur bloquante)");
        Check(output.Contains("SDD-L001", StringComparison.Ordinal) && output.Contains("manque « Alors »", StringComparison.Ordinal),
              "L001 signale Alors manquant");
        Check(output.Contains("1 erreurs", StringComparison.Ordinal), "compteur erreurs = 1");
    }

    private static void Test_L002_DecisionSansStatut(string sandbox)
    {
        Console.WriteLine("T8 — SDD-L002 : décision sans statut");
        string dir = MakeFixture(sandbox, "l002",
            "# Spec\n## Historique\n- v1.0 : x\n\n## Décisions\n\n- **D1** ratifiée — RATIFIÉE 16/09\n- **D2** sans rien\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 1, "exit 1");
        Check(output.Contains("✖ SDD-L002  décision sans statut : D2 (SPEC-T)", StringComparison.Ordinal),
              "format exact ligne D2 (SPEC-T)");
    }

    private static void Test_L003_RefFantasma_Et_Waiver(string sandbox)
    {
        Console.WriteLine("T9 — SDD-L003 : référence fantasma + waiver");
        string spec = "# Spec\n## Historique\n- v1.0 : x\n\n## Notes\n\nVoir SPEC-FANTOME.md pour la suite.\n";
        string dir = MakeFixture(sandbox, "l003", spec);
        var (exit, output) = RunLint(dir);
        Check(exit == 1, "sortie hors waiver → erreur");
        Check(output.Contains("✖ SDD-L003  référence fantasma", StringComparison.Ordinal), "ligne règle");
        Check(output.Contains("cite SPEC-FANTOME.md —", StringComparison.Ordinal), "ligne localisée (fichier:ligne)");
        Check(output.Contains("absente de docs/specs/", StringComparison.Ordinal), "ligne détail 2");

        string waivedDir = MakeFixture(sandbox, "l003-w", spec, tomlExtra:
            "\n[[waiver]]\nregle = \"SDD-L003\"\nportee = \"docs/specs/SPEC-T.md\"\njustification = \" externe \"\n");
        var (exit2, out2) = RunLint(waivedDir);
        Check(exit2 == 0, "avec waiver → exit 0");
        Check(out2.Contains("0 erreurs", StringComparison.Ordinal) && out2.Contains("1 waivers", StringComparison.Ordinal),
              "0 erreurs · 1 waivers");
    }

    private static void Test_L004_BacklogSansOrigine(string sandbox)
    {
        Console.WriteLine("T10 — SDD-L004 : entrée BACKLOG sans origine ni statut");
        string dir = MakeFixture(sandbox, "l004",
            "# Spec\n## Historique\n- v1.0 : x\n",
            backlog: "- [ ] BUK-999 — idée floue sans date\n- [ ] BUK-001 (2026-09-15, origine : ok) correcte → P1\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 1, "exit 1");
        Check(output.Contains("SDD-L004", StringComparison.Ordinal) && output.Contains("BUK-999", StringComparison.Ordinal),
              "L004 vise BUK-999 seulement");
        Check(output.Contains("1 erreurs", StringComparison.Ordinal), "BUK-001 conformne non compte");
    }

    private static void Test_L005_PhaseSansJalon(string sandbox)
    {
        Console.WriteLine("T11 — SDD-L005 : phase sans jalon visible");
        string dir = MakeFixture(sandbox, "l005",
            "# Spec\n## Historique\n- v1.0 : x\n\n## Phases\n\n| Phase | Contenu | Jalon visible | Statut |\n|---|---|---|---|\n| P1 | a |  | ratifiée |\n| P2 | b | jalon ok | à approuver |\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 1, "exit 1");
        Check(output.Contains("SDD-L005", StringComparison.Ordinal) && output.Contains("P1 —", StringComparison.Ordinal),
              "L005 vise la ligne P1");
    }

    private static void Test_L006_MagicNumbers(string sandbox)
    {
        Console.WriteLine("T12 — SDD-L006 : magic numbers (warning)");
        string dir = MakeFixture(sandbox, "l006",
            "# Spec\n## Historique\n- v1.0 : x\n\n```csharp\nvar timeout = 86400;\nvar annee = 2026;\n```\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 0, "warning ne bloque pas (exit 0)");
        Check(output.Contains("SDD-L006", StringComparison.Ordinal) && output.Contains("86400", StringComparison.Ordinal),
              "L006 cite 86400");
        Check(!output.Contains("2026 (", StringComparison.Ordinal), "années tolérées");
        Check(output.Contains("1 warnings", StringComparison.Ordinal), "compteur warnings");
    }

    private static void Test_L007_SansHistorique(string sandbox)
    {
        Console.WriteLine("T13 — SDD-L007 : spec sans Historique (warning)");
        string dir = MakeFixture(sandbox, "l007", "# Spec\n\n## Propos\n\nx\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 0, "exit 0");
        Check(output.Contains("SDD-L007", StringComparison.Ordinal), "L007 signalée en warning");
    }

    private static void Test_Lint_Propre_Exit0(string sandbox)
    {
        Console.WriteLine("T14 — projet généré propre : 0 erreurs, format résumé");
        string dir = FreshProject(sandbox, "lint-clean");
        RunCli(dir, "init", "--projet", "t");
        RunCli(dir, "new", "EXEMPLE");
        var (exit, output) = RunLint(dir);
        Check(exit == 0, "exit 0");
        Check(output.Contains("0 erreurs", StringComparison.Ordinal) && output.Contains("0 waivers", StringComparison.Ordinal),
              "résumé 0 erreurs · 0 waivers");
        Check(!output.Contains("warnings", StringComparison.Ordinal), "pas de champ warnings si 0 (conformité Annexe A)");
    }

    private static void Test_Status_ByteIdentique_AnnexeA(string sandbox)
    {
        Console.WriteLine("T15 — sdd status byte-identique à Annexe A (fixture)")
            ;
        string dir = FreshProject(sandbox, "status-norm");
        Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
        File.WriteAllText(Path.Combine(dir, "sdd.toml"),
            "[projet]\nnom = \"mon-projet\"\n\n[doctrine]\nversion = \"1.0\"\n");

        string Deco(string name, string statut, string decs) =>
            $"# Spec {name}\n**Statut** : {statut}\n\n## Historique\n- v1.0 (2026-09-15) : x\n\n## Décisions\n\n{decs}\n";
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-A.md"),
            Deco("A", "Approuvée", "- **D1** a — RATIFIÉE\n- **D2** b — RATIFIÉE\n- **D3** c — différée (P3)\n"));
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-B.md"),
            Deco("B", "Approuvée", "- **D1** a — RATIFIÉE\n- **D2** b — RATIFIÉE\n- **D3** c — différée (P4)\n"));
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-C.md"),
            Deco("C", "Brouillon", "- **D1** a — RATIFIÉE\n- **D2** b — RATIFIÉE\n"));
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-EXEMPLE.md"),
            Deco("EXEMPLE", "En phase", "- **D1** z — ouverte\n")
            + "\n## Phases\n\n| Phase | Contenu | Jalon visible | Statut |\n|---|---|---|---|\n"
            + "| P1 | Composant X | livraison composant | **approuvée** |\n"
            + "| P2 | Intégration | intégration en production | à approuver |\n");

        File.WriteAllText(Path.Combine(dir, "docs", "BACKLOG.md"),
            "# BACKLOG\n\n## Entrées\n\n"
            + string.Concat(Enumerable.Range(1, 5).Select(i => $"- [ ] BUK-00{i} (2026-09-15, origine : t) x → P{i}\n"))
            + string.Concat(Enumerable.Range(6, 3).Select(i => $"- [x] BUK-00{i} (2026-09-15, origine : t) y → CLOS\n")));

        File.WriteAllText(Path.Combine(dir, "docs", "AGENT_STATE.md"),
            """
            # AGENT_STATE — mon-projet

            ## État courant

            | Champ | Valeur |
            |---|---|
            | Spec de référence | SPEC-EXEMPLE |
            | Phase active | P1 |
            | Progression | 60 % |

            ## Tâches bornées

            """ + string.Concat(Enumerable.Range(1, 11).Select(_ => "- [x] t\n"))
            + "- [ ] t-rouge\n");

        var buf = new StringWriter();
        Status.Run(dir, buf);
        string[] rendered = buf.ToString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        string[] sample = Sample("status-sample.txt"); // lignes 1..11 = box complet
        Check(rendered.Length == sample.Length, $"nombre de lignes {rendered.Length} == {sample.Length}");
        bool identical = rendered.Length == sample.Length;
        for (int i = 0; identical && i < sample.Length; i++)
        {
            if (rendered[i] != sample[i])
            {
                identical = false;
                Console.WriteLine($"    ligne {i + 1} diffère :\n      rendu : «{rendered[i]}»\n      attendu : «{sample[i]}»");
            }
        }

        Check(identical, "status byte-identique à l'Annexe A");
    }

    private static void Test_Dogfood_SddKit(string startup)
    {
        Console.WriteLine("T16 — dogfooding : lint propre sur le repo sdd-kit lui-même");
        string repoSpecs = Path.Combine(startup, "docs", "specs", "SPEC-OUTIL-SDD.md");
        if (!File.Exists(repoSpecs))
        {
            Console.WriteLine("  (non exécuté hors racine du repo — lancer `dotnet run` depuis ~/sdd-kit)");
            return;
        }

        var (exit, output) = RunLint(startup);
        Check(exit == 0, "sdd lint repo → exit 0 (0 erreurs)");
        Check(output.Contains("0 erreurs", StringComparison.Ordinal), "0 erreurs");
        Check(output.Contains("1 waivers", StringComparison.Ordinal), "1 waiver justifié (SPEC-CAS-VIZ-LIGNAGE externe)");

        var buf = new StringWriter();
        Status.Run(startup, buf);
        string[] lines = buf.ToString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Check(lines[0].StartsWith('┌') && lines[^1].StartsWith('└'), "status repo : box complet");
        Check(lines.All(l => l.Length == 64), "toutes les lignes du box à 64 caractères");
        Check(lines.Any(l => l.Contains("◐ 100 %", StringComparison.Ordinal)),
              "progression réelle : P1–P4 toutes approuvées (P3 ratifiée par le responsable 16/09) → ◐ 100 %");
    }

    // ---------- P3 : decide / trace / agent-brief ----------

    private static string InitGitProject(string sandbox, string label)
    {
        string dir = FreshProject(sandbox, label);
        RunCli(dir, "init", "--projet", "portail-citoyen");
        RunCli(dir, "new", "PORTAIL-CITOYEN");
        Git.Run(dir, "init", "-b", "main");
        Git.Run(dir, "config", "user.name", "sdd-tests");
        Git.Run(dir, "config", "user.email", "tests@local");
        Git.Run(dir, "add", "-A");
        Git.Run(dir, "commit", "-m", "chore(sdd): init — artefacts doctrine v1.0");
        return dir;
    }

    private static void Test_Decide(string sandbox)
    {
        Console.WriteLine("T17 — sdd decide : ligne + Historique + commit atomique");
        string dir = InitGitProject(sandbox, "decide");
        var (exit, _) = RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1",
            "Distribution par dotnet tool interne.", "--ratifiee");
        Check(exit == 0, "exit 0");

        string spec = Read(Path.Combine(dir, "docs", "specs", "SPEC-PORTAIL-CITOYEN.md"));
        Check(spec.Contains("- **D1** Distribution par dotnet tool interne. — **RATIFIÉE ", StringComparison.Ordinal),
              "ligne D1 = texte du responsable + RATIFIÉE + date");
        Check(spec.Contains("**Version** : 1.1", StringComparison.Ordinal), "bump version mineure 1.0 → 1.1");
        Check(Regex.IsMatch(spec, @"- v1\.1 \(\d{2}/\d{2}/\d{4}\) : décision D1 ratifiée par le responsable"),
              "Historique appendu (dd/MM/yyyy)");
        Check(RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "x", "--ratifiee").exit == 0, "re-décide idempotent-ish (exit 0)");
        string log = Git.Run(dir, "log", "--format=%s").stdout;
        Check(log.Contains("docs(portail-citoyen): ratification D1 — v1.1", StringComparison.Ordinal),
              "commit atomique généré");
        Check(RunCli(dir, "decide", "SPEC-INEXISTANTE", "D1", "x", "--ratifiee").exit != 0, "spec inconnue → erreur");
        Check(RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D9", "x", "--ratifiee").exit != 0, "D9 absent → erreur");
    }

    private static void Test_Trace(string sandbox)
    {
        Console.WriteLine("T18 — sdd trace : commits + statut global");
        string dir = InitGitProject(sandbox, "trace");
        var (exit0, out0) = (RunCli(dir, "trace", "REQ-PORTAIL-CITOYEN01").exit, Capture(dir, "trace", "REQ-PORTAIL-CITOYEN01"));
        Check(exit0 == 0, "exit 0");
        Check(out0.Contains("Commits mentionnant la REQ : 0", StringComparison.Ordinal)
              && out0.Contains("non démarrée", StringComparison.Ordinal), "0 commit → non démarrée");

        Git.Run(dir, "commit", "--allow-empty", "-m", "feat(cli): Implements REQ-PORTAIL-CITOYEN01");
        string out1 = Capture(dir, "trace", "REQ-PORTAIL-CITOYEN01");
        Check(out1.Contains("Commits mentionnant la REQ : 1", StringComparison.Ordinal), "1 commit trouvé");
        Check(out1.Contains("Implements REQ-PORTAIL-CITOYEN01", StringComparison.Ordinal), "sujet du commit listé");
        Check(out1.Contains("partielle", StringComparison.Ordinal), "phase non approuvée → partielle");
        Check(RunCli(dir, "trace", "REQ-FANTAISIE99").exit != 0, "REQ hors spec → erreur");
    }

    private static string Capture(string dir, params string[] args)
    {
        string previous = Directory.GetCurrentDirectory();
        var buf = new StringWriter();
        TextWriter o = Console.Out, e = Console.Error;
        Console.SetOut(buf);
        Console.SetError(buf);
        try
        {
            Directory.SetCurrentDirectory(dir);
            Program.Main(args);
        }
        finally
        {
            Console.SetOut(o);
            Console.SetError(e);
            Directory.SetCurrentDirectory(previous);
        }

        return buf.ToString();
    }

    private static void Test_AgentBrief(string sandbox)
    {
        Console.WriteLine("T19 — sdd agent-brief : prompt auto-suffisant");
        string dir = InitGitProject(sandbox, "brief");
        RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "dotnet tool.", "--ratifiee");
        string outp = Capture(dir, "agent-brief", "SPEC-PORTAIL-CITOYEN", "P1", "--pour", "qwenwork");
        Check(outp.Contains("# Tâche bornée : SPEC-PORTAIL-CITOYEN — P1", StringComparison.Ordinal), "titre");
        Check(outp.Contains("## Périmètre strict", StringComparison.Ordinal) && outp.Contains("Exclus (ne pas toucher", StringComparison.Ordinal),
              "périmètre inclus/exclus");
        Check(outp.Contains("— **RATIFIÉE", StringComparison.Ordinal), "décision ratifiée incrustée");
        Check(outp.Contains("1. **Spec avant code**", StringComparison.Ordinal), "doctrine incrustée (verbatim docs/DOCTRINE.md)");
        Check(outp.Contains("## Format de commit attendu", StringComparison.Ordinal)
              && outp.Contains("## Format de report attendu", StringComparison.Ordinal), "formats commit+report");
        Check(outp.Contains("destinataire : qwenwork", StringComparison.Ordinal), "destinataire");
        Check(outp.Contains("Validation", StringComparison.Ordinal), "section validation");
    }

    private static void Test_AgentBrief_Dogfood_P3(string startup)
    {
        Console.WriteLine("T20 — agent-brief dogfooding sur SPEC-OUTIL-SDD P3");
        if (!File.Exists(Path.Combine(startup, "docs", "specs", "SPEC-OUTIL-SDD.md")))
        {
            Console.WriteLine("  (ignoré hors racine du repo)");
            return;
        }

        string outp = Capture(startup, "agent-brief", "SPEC-OUTIL-SDD", "P3", "--pour", "qwenwork");
        Check(outp.Contains("### REQ-CLI06", StringComparison.Ordinal)
              && outp.Contains("### REQ-CLI07", StringComparison.Ordinal)
              && outp.Contains("### REQ-CLI08", StringComparison.Ordinal),
              "les 3 REQ de P3 (decide/trace/agent-brief) sont incrustées");
        Check(!outp.Contains("### REQ-CLI01", StringComparison.Ordinal), "REQ de P1 exclues du brief P3");
        Check(outp.Split("- **D").Length - 1 >= 7, "les 7 décisions ratifiées sont incrustées");
        Check(outp.Contains("contrat humain↔agent généré", StringComparison.Ordinal), "jalon P3 présent");
    }

    // ---------- P4 : adopt + CI ----------

    private static void Test_Adopt_Brownfield(string sandbox)
    {
        Console.WriteLine("T21 — sdd adopt : détection des 5 patterns (hors-ligne pour les liens)");
        string dir = FreshProject(sandbox, "adopt");
        Directory.CreateDirectory(Path.Combine(dir, "Pages"));
        Directory.CreateDirectory(Path.Combine(dir, "Components"));
        Directory.CreateDirectory(Path.Combine(dir, "wwwroot"));
        File.WriteAllText(Path.Combine(dir, "Pages", "Accueil.razor"),
            "@page \"/accueil\"\n<h1>Accueil</h1>\n");
        File.WriteAllText(Path.Combine(dir, "Pages", "Dashboard.razor"),
            "@page \"/tableau-de-bord\"\n<h1>Dashboard</h1>\n");
        File.WriteAllText(Path.Combine(dir, "Components", "NavMenu.razor"),
            "<nav><NavLink href=\"/accueil\">Accueil</NavLink></nav>\n");
        File.WriteAllText(Path.Combine(dir, "Pages", "Rapport.razor"),
            "@page \"/rapport\"\n@* // composant de rapport *@\n"
            + "// TODO: brancher l'API réelle\n"
            + "var chart = new Chart(el, { data: { datasets: [] } });\n"
            + "ShowToast(Success, \"✔ Export terminé (0 éléments)\");\n");
        File.WriteAllText(Path.Combine(dir, "Pages", "Kpis.razor"),
            "@page \"/kpis\"\n<p>100 % des dossiers numérisés</p>\n<p>0 items traités</p>\n");
        File.WriteAllText(Path.Combine(dir, "wwwroot", "index.html"),
            "<html><body><a href=\"https://exemple-introuvable-sdd.test/404\">lien</a></body></html>\n");

        Environment.SetEnvironmentVariable("SDD_SKIP_NETWORK", "1");
        (int exit, string output) run;
        try
        {
            run = RunCli(dir, "adopt", "--projet", "portail-citoyen");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SDD_SKIP_NETWORK", null);
        }

        int exit = run.exit;
        string output = run.output;

        Check(exit == 0, "adopt exit 0");
        Check(output.Contains("route orpheline", StringComparison.Ordinal), "sortie : route orpheline")
            ;
        Check(output.Contains("hors-ligne", StringComparison.Ordinal), "liens non vérifiés déclarés hors-ligne (zéro faux positif)");

        string backlog = Read(Path.Combine(dir, "docs", "BACKLOG.md"));
        Check(backlog.Contains("route orpheline", StringComparison.Ordinal), "BUK route orpheline (/tableau-de-bord)");
        Check(backlog.Contains("TODO/FIXME", StringComparison.Ordinal), "BUK mock TODO");
        Check(backlog.Contains("sans données réelles", StringComparison.Ordinal), "BUK chart sans données");
        Check(backlog.Contains("fausse confirmation", StringComparison.Ordinal), "BUK fausse confirmation (succès N=0)");
        Check(backlog.Contains("métriques contradictoires", StringComparison.Ordinal), "BUK métriques 100 % vs 0");
        Check(backlog.Contains("- [ ] BUK-001", StringComparison.Ordinal) && backlog.Contains("2026-", StringComparison.Ordinal)
              && backlog.Contains("sdd adopt", StringComparison.Ordinal), "entrées BUK avec date + origine + statut ouvert");
        Check(File.Exists(Path.Combine(dir, "sdd.toml")) && File.Exists(Path.Combine(dir, "docs", "DOCTRINE.md")),
              "infrastructures SDD créées (DOCTRINE, BACKLOG, AGENT_STATE, sdd.toml, workflow)");
        Check(File.ReadAllText(Path.Combine(dir, "Pages", "Dashboard.razor")).Contains("@page", StringComparison.Ordinal),
              "code existant intact (aucune modification du projet)");
    }

    private static void Test_Adopt_DejaInitialise(string sandbox)
    {
        Console.WriteLine("T22 — sdd adopt refuse un projet déjà initialisé");
        string dir = FreshProject(sandbox, "adopt-twice");
        Directory.CreateDirectory(Path.Combine(dir, "Pages"));
        File.WriteAllText(Path.Combine(dir, "Pages", "A.razor"), "@page \"/a\"\n");
        Check(RunCli(dir, "adopt", "--projet", "p").exit == 0, "premier adopt OK");
        var (exit, output) = RunCli(dir, "adopt", "--projet", "p");
        Check(exit != 0, "second adopt refusé");
    }

    private static void Test_Lint_CI(string sandbox)
    {
        Console.WriteLine("T23 — sdd lint --ci : format condensé + exit codes");
        string dir = FreshProject(sandbox, "ci");
        RunCli(dir, "init", "--projet", "t");
        RunCli(dir, "new", "EXEMPLE");
        string outp = Capture(dir, "lint", "--ci");
        Check(outp.Contains("RESUME regles_ok=", StringComparison.Ordinal) && outp.Contains("erreurs=0", StringComparison.Ordinal),
              "propre : RESUME erreurs=0");
        Check(!outp.Contains("·", StringComparison.Ordinal), "pas de format normatif humain en mode --ci");
        Check(outp.TrimEnd().Length > 0 && outp.Split('\n').All(l => !l.StartsWith('✔')), "aucune sortie box/humaine");

        string specPath = Path.Combine(dir, "docs", "specs", "SPEC-EXEMPLE.md");
        string spec = Read(specPath).Replace("**Alors** [À RATIFIER]", "// Alors retiré");
        File.WriteAllText(specPath, spec);
        string outp2 = Capture(dir, "lint", "--ci");
        Check(outp2.Contains("SDD-L001 ERREUR", StringComparison.Ordinal) && outp2.Contains("erreurs=1", StringComparison.Ordinal),
              "erreur cassante : ligne condensée SDD-L001 ERREUR + erreurs=1");
        Check(CaptureExit(dir) == 1, "exit code 1 quand ≥1 erreur");
        string? gha = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
            Check(!Capture(dir, "lint", "--ci").Contains("::error", StringComparison.Ordinal),
                  "hors CI : pas d'annotations GHA (GITHUB_ACTIONS non défini)");
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
            Check(Capture(dir, "lint", "--ci").Contains("::error", StringComparison.Ordinal),
                  "dans CI : annotations ::error émises pour chaque erreur");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", gha);
        }
    }

    private static int CaptureExit(string dir)
    {
        string previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            return Program.Main(new[] { "lint", "--ci" });
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    // ---------- micro-fix : usage / --version / adopt vide ----------

    private static void Test_Usage_Honnete()
    {
        Console.WriteLine("T24 — help honnête : 8 commandes, aucune promesse future");
        string help = Capture(Directory.GetCurrentDirectory(), "help");
        foreach (string cmd in new[] { "sdd init", "sdd adopt", "sdd new", "sdd lint", "sdd status", "sdd decide", "sdd trace", "sdd agent-brief" })
        {
            Check(help.Contains(cmd, StringComparison.Ordinal), $"help liste {cmd}");
        }

        Check(!help.Contains("Reste à venir", StringComparison.Ordinal), "aucune annonce de phase future");
        Check(help.Contains("--version", StringComparison.Ordinal), "help mentionne --version");
    }

    private static void Test_Version()
    {
        Console.WriteLine("T25 — sdd --version / -v (source unique : version de l'assembly)");
        string attendue = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "";
        string v1 = Capture(Directory.GetCurrentDirectory(), "--version");
        string v2 = Capture(Directory.GetCurrentDirectory(), "-v");
        Check(v1.Contains($"sdd {attendue}", StringComparison.Ordinal), $"--version → sdd {attendue}");
        Check(v2.Contains($"sdd {attendue}", StringComparison.Ordinal), "-v idem (aucune littérale dupliquée)");
        Check(attendue == "1.0.4", "l'assembly est bien 1.0.4 (garde anti-dérive du csproj)");
        Check(v1.Contains("doctrine v1.0", StringComparison.Ordinal), "pin doctrine affiché");
    }

    private static void Test_Adopt_Vide(string sandbox)
    {
        Console.WriteLine("T26 — sdd adopt sans hallazgo : pas de rang vide");
        string dir = FreshProject(sandbox, "adopt-vide");
        var (exit, output) = RunCli(dir, "adopt", "--projet", "propre");
        Check(exit == 0, "exit 0");
        Check(output.Contains("aucune entrée BACKLOG créée", StringComparison.Ordinal), "message explicite");
        Check(!output.Contains("BUK-001…BUK-000", StringComparison.Ordinal), "rang vide supprimé");
        Check(Read(Path.Combine(dir, "docs", "BACKLOG.md")).Contains("_(vide)_", StringComparison.Ordinal), "BACKLOG reste vide");
    }

    private static void Test_I18n_Garde(string sandbox)
    {
        Console.WriteLine("T27 — garde i18n (liste noire unique, extensible dans Lint.LangBlacklist)");
        string[] texts =
        {
            Program.Resource("DOCTRINE.md"),
            Program.Resource("SPEC-template.md"),
            Scaffold.Backlog("t", null),
            Scaffold.AgentState("t"),
            Scaffold.Toml("t", null),
        };
        foreach (string t in Lint.LangBlacklist)
        {
            foreach (string text in texts)
            {
                Check(!text.Contains(t, StringComparison.OrdinalIgnoreCase), $"resource sans « {t} »");
            }
        }

        string dirVide = FreshProject(sandbox, "i18n-adopt");
        var (_, adoptOut) = RunCli(dirVide, "adopt", "--projet", "propre");
        foreach (string t in Lint.LangBlacklist)
        {
            Check(!adoptOut.Contains(t, StringComparison.OrdinalIgnoreCase), $"sortie adopt sans « {t} »");
        }

        string dirDet = MakeFixture(sandbox, "l008",
            "# Spec\n## Historique\n- v1.0 : x\n\n## Notes\n\nIdée pospuesta et un hallazgo isolé, plus des hallazgos divers, ratifié par l'owner ; « Lee … y continúa » et sa cabecera aussi.\n");
        var (exit, output) = RunLint(dirDet);
        Check(exit == 0, "L008 est un warning : ne bloque pas");
        Check(output.Contains("SDD-L008", StringComparison.Ordinal)
              && output.Contains("langue non conforme au pin D3", StringComparison.Ordinal),
              "SDD-L008 signalé (un warning par terme présent)");
        Check(output.Contains("6 warnings", StringComparison.Ordinal), "6 warnings — tous les termes de la liste noire détectés (BUK-017)");
    }

    private static void Test_Decide_Idempotence(string sandbox)
    {
        Console.WriteLine("T28 — sdd decide idempotent (micro-fix 1.0.2)");
        string dir = InitGitProject(sandbox, "idem");
        string specPath = Path.Combine(dir, "docs", "specs", "SPEC-PORTAIL-CITOYEN.md");
        int RatifEntries() => Read(specPath).Split('\n').Count(l => l.Contains(": décision D1 ratifiée", StringComparison.Ordinal));

        var (e1, _) = RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "Même texte.", "--ratifiee");
        Check(e1 == 0, "decide #1 appliqué");
        Check(RatifEntries() == 1, "1 entrée Historique après #1");

        var (e2, out2) = RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "Même texte.", "--ratifiee");
        Check(e2 == 0, "decide #2 (texte identique) → exit 0");
        Check(out2.Contains("no-op", StringComparison.Ordinal), "decide #2 déclaré no-op");
        Check(RatifEntries() == 1, "toujours 1 seule entrée Historique (zéro duplicat)");
        Check(Read(specPath).Contains("**Version** : 1.1", StringComparison.Ordinal), "un seul bump (v1.1)")
            ;
        int commits = Git.Run(dir, "rev-list", "--count", "HEAD").stdout.Trim() is var n && int.TryParse(n, out int c) ? c : 0;
        RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "Même texte.", "--ratifiee");
        int commitsAfterNoop = Git.Run(dir, "rev-list", "--count", "HEAD").stdout.Trim() is var n2 && int.TryParse(n2, out int c2) ? c2 : 0;
        Check(commitsAfterNoop == commits, "no-op ne crée aucun commit");

        var (e3, _) = RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "Même texte.", "--ratifiee", "--force");
        Check(e3 == 0 && RatifEntries() == 2, "--force réédite (2e entrée)");
        var (e4, _) = RunCli(dir, "decide", "SPEC-PORTAIL-CITOYEN", "D1", "Texte différent.", "--ratifiee");
        Check(e4 == 0 && RatifEntries() == 3, "texte différent → update + 3e entrée normalement");
    }

    // ---------- micro-fix audit externe DeepSeek (BUK-009/010/011) ----------

    private static void Test_Adopt_BacklogPreexistant(string sandbox)
    {
        Console.WriteLine("T29 — adopt sur BACKLOG préexistant : append honnête (BUK-009)");
        string dir = FreshProject(sandbox, "adopt-append");
        Directory.CreateDirectory(Path.Combine(dir, "docs"));
        Directory.CreateDirectory(Path.Combine(dir, "Pages"));
        string original =
            "# BACKLOG — legacy\n\n## Entrées\n\n- [ ] BUK-777 (2026-09-01, origine : audit manuel) dette existante → P1\n";
        File.WriteAllText(Path.Combine(dir, "docs", "BACKLOG.md"), original);
        File.WriteAllText(Path.Combine(dir, "Pages", "Rapport.razor"),
            "@page \"/rapport\"\n// TODO: brancher l'API\n");
        Environment.SetEnvironmentVariable("SDD_SKIP_NETWORK", "1");
        var (exit, output) = RunCli(dir, "adopt", "--projet", "legacy");
        Environment.SetEnvironmentVariable("SDD_SKIP_NETWORK", null);

        Check(exit == 0, "adopt exit 0");
        string after = Read(Path.Combine(dir, "docs", "BACKLOG.md"));
        Check(after.Contains("BUK-777", StringComparison.Ordinal) && after.Contains("dette existante", StringComparison.Ordinal),
              "contenu préexistant INTACT (pas d'écrasement)");
        Check(after.Contains("<!-- sdd-adopt legacy", StringComparison.Ordinal), "marqueur d'append présent");
        Check(after.Contains("- [ ] BUK-001", StringComparison.Ordinal), "BUK de l'audit APPENDU (pas jeté)");
        Check(output.Contains("enregistrés", StringComparison.Ordinal) && after.Contains("BUK-001", StringComparison.Ordinal),
              "le résumé « enregistrés » est vrai : les entrées sont sur disque");
        Check(output.Contains("mis à jour (append sous marqueur)", StringComparison.Ordinal), "log honnête pour le fichier préexistant");
    }

    private static void Test_Workflow_Installable(string startup)
    {
        Console.WriteLine("T30 — workflow généré : guard d'installation, jamais de « command not found » nu (BUK-010)");
        string wf = Scaffold.Workflow();
        Check(wf.Contains("command -v sdd", StringComparison.Ordinal), "garde command -v sdd présente");
        Check(wf.Contains("exit 2", StringComparison.Ordinal) && wf.Contains("introuvable", StringComparison.Ordinal),
              "sinon message lisible + exit ≠ 0");
        Check(!Regex.IsMatch(wf, @"else\s*\n\s*sdd lint --ci"), "aucun appel brut `sdd` non gardé");
        string repoWf = Path.Combine(startup, ".github", "workflows", "sdd-lint.yml");
        if (File.Exists(repoWf))
        {
            Check(Read(repoWf).TrimEnd() == wf.TrimEnd(), "le workflow du propre repo sdd-kit est à jour de la template");
        }
    }

    private static void Test_Decide_SpecIncomplete(string sandbox)
    {
        Console.WriteLine("T31 — decide sur spec sans Version/Historique : refus net, document intact (BUK-011)");
        string dir = FreshProject(sandbox, "decide-incomplete");
        Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
        string content = "# Spec LEGACY\n\n## Décisions\n\n- **D1** choix hérité — à ratifier\n";
        string specPath = Path.Combine(dir, "docs", "specs", "SPEC-LEGACY.md");
        File.WriteAllText(specPath, content);
        byte[] before = File.ReadAllBytes(specPath);
        var (exit, output) = RunCli(dir, "decide", "SPEC-LEGACY", "D1", "choix hérité", "--ratifiee");
        Check(exit != 0, "exit ≠ 0");
        Check(output.Contains("**Version**", StringComparison.Ordinal) && output.Contains("## Historique", StringComparison.Ordinal),
              "l'erreur nomme les DEUX sections manquantes");
        Check(File.ReadAllBytes(specPath).SequenceEqual(before), "document byte-idéntique (aucune corruption)");
    }

    // ---------- micro-fix audit externe II (BUK-012..015) ----------

    private static void Test_Status_SansSpecEnVol(string sandbox)
    {
        Console.WriteLine("T32 — status sans spec en vol : EN VOL — , JALON — , compteurs = Statut réel (BUK-012)");
        string dir = FreshProject(sandbox, "status-vol");
        Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
        File.WriteAllText(Path.Combine(dir, "sdd.toml"), "[projet]\nnom = \"t\"\n\n[doctrine]\nversion = \"1.0\"\n");
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-ALPHA.md"),
            "**Statut** : Brouillon\n\n## Historique\n- v1.0 : x\n");
        File.WriteAllText(Path.Combine(dir, "docs", "specs", "SPEC-BETA.md"),
            "**Statut** : Approuvée\n\n## Historique\n- v1.0 : x\n");
        File.WriteAllText(Path.Combine(dir, "docs", "AGENT_STATE.md"),
            "| Champ | Valeur |\n|---|---|\n| Spec de référence | — |\n| Phase active | — |\n");
        var buf = new StringWriter();
        Status.Run(dir, buf);
        string outp = buf.ToString().Replace("\r\n", "\n");
        Check(outp.Contains("EN VOL   —", StringComparison.Ordinal) && !outp.Contains("◐", StringComparison.Ordinal),
              "aucune spec en vol : « — » sans progression inventée");
        Check(outp.Contains("JALON    —", StringComparison.Ordinal), "jalon — aussi");
        Check(outp.Contains("Brouillon 1 · Approuvée 1 · En phase 0", StringComparison.Ordinal),
              "compteurs issus UNIQUEMENT des **Statut** (la présence de fichiers SPEC-* ne force rien)");
    }

    private static void Test_L003_DocsAnidados(string sandbox)
    {
        Console.WriteLine("T33 — L003 sur docs anidé (option a : glob récursif) (BUK-013)");
        string dir = MakeFixture(sandbox, "nested", "# Spec\n## Historique\n- v1.0 : x\n");
        Directory.CreateDirectory(Path.Combine(dir, "docs", "guides"));
        File.WriteAllText(Path.Combine(dir, "docs", "guides", "note.md"),
            "# Guide\n\nSe référer à SPEC-FANTOME.md pour le détail.\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 1, "docs/guides/note.md n'est plus invisible : exit 1");
        Check(output.Contains("SDD-L003", StringComparison.Ordinal) && output.Contains("guides/note.md", StringComparison.Ordinal),
              "l'erreur localise guides/note.md");
    }

    private static void Test_WaiverInutilise(string sandbox)
    {
        Console.WriteLine("T34 — waiver SDD-L999 jamais déclenché : audible (BUK-014)");
        string dir = MakeFixture(sandbox, "waiver-typo", "# Spec\n## Historique\n- v1.0 : x\n",
            tomlExtra: "\n[[waiver]]\nregle = \"SDD-L999\"\nportee = \"\"\njustification = \"coquille volontaire pour le test \"\n");
        var (exit, output) = RunLint(dir);
        Check(exit == 0, "avertissement, pas blocant (politique des warnings)");
        Check(output.Contains("SDD-WVR", StringComparison.Ordinal) && output.Contains("SDD-L999", StringComparison.Ordinal),
              "le waiver typo est nommément listé");
        Check(Regex.IsMatch(output, @"sdd\.toml:\d+ waiver « SDD-L999 »"), "avec la ligne exacte du sdd.toml")
            ;
        Check(output.Contains("1 warnings", StringComparison.Ordinal), "comptabilisé comme warning");
    }

    private static void Test_Workflow_AvecTests()
    {
        Console.WriteLine("T35 — le gate CI court les tests (BUK-015)");
        string wf = Scaffold.Workflow();
        Check(wf.Contains("dotnet run --project tests/Sdd.Tests", StringComparison.Ordinal),
              "étape de tests présente dans la template");
        Check(wf.IndexOf("tests SDD", StringComparison.Ordinal) < wf.IndexOf("sdd lint --ci", StringComparison.Ordinal),
              "les tests tournent avant le lint");
    }

    // ---------- cosmétique finale (BUK-016…022) ----------

    private static void Test_Args_Validees(string sandbox)
    {
        Console.WriteLine("T36 — arguments validés (jamais ignorés en silence) (BUK-020)");
        string dir = FreshProject(sandbox, "args");
        var (s1, so1) = RunCli(dir, "status", "foo");
        Check(s1 != 0 && so1.Contains("argument inconnu", StringComparison.Ordinal), "sdd status foo → erreur explicite");
        var (s2, so2) = RunCli(dir, "lint", "--xyz");
        Check(s2 != 0 && so2.Contains("arguments inconnus", StringComparison.Ordinal), "sdd lint --xyz → erreur explicite");
        var (s3, so3) = RunCli(dir, "lint", "--help");
        Check(s3 == 0 && so3.Contains("Usage :", StringComparison.Ordinal), "sdd lint --help → usage, exit 0");
        var (s4, _) = RunCli(dir, "lint", "--ci", "--xyz");
        Check(s4 != 0, "mélange --ci + flag inconnu refusé");
        var (s5, _) = RunCli(dir, "lint", "--ci");
        Check(s5 == 0 || s5 == 1, "sdd lint --ci toujours accepté (5/6 exit selon projet)");
    }

    private static void Test_TomlMultiLigne(string sandbox)
    {
        Console.WriteLine("T37 — sdd.toml : chaines multilignes (BUK-022)");
        string dir = FreshProject(sandbox, "toml-ml");
        string[] tomlLines =
        {
            "[projet]",
            "nom = \"\"\"",
            "portail-citoyen",
            "(ligne ajoutée)",
            "\"\"\"",
            "",
            "[doctrine]",
            "version = '''1.0'''",
            "",
            "[[waiver]]",
            "regle = \"SDD-L999\"",
            "portee = ''''''",
            "justification = '''",
            "justification",
            "sur deux lignes",
            "'''",
        };
        File.WriteAllText(Path.Combine(dir, "sdd.toml"), string.Join("\n", tomlLines) + "\n");
        var meta = TomlLite.Load(dir);
        Check(meta.Nom == "portail-citoyen\n(ligne ajoutée)", "nom multiligne basique assemblé");
        Check(meta.Doctrine == "1.0", "simple quotes literals supportées");
        Check(meta.Waivers.Count == 1 && meta.Waivers[0].Justification == "justification\nsur deux lignes",
              "justification multiligne littérale assemblée");
    }

    private static void Test_PhaseMentions_Partage(string sandbox)
    {
        Console.WriteLine("T38 — PhaseMentions : une seule implémentation, comportement d'origine (BUK-021)");
        string dir = MakeFixture(sandbox, "pm",
            "# Spec\n**Version** : 1.0\n## Historique\n- v1.0 : x\n\n## Exigences\n\n"
            + "### REQ-PM01 : Initialisation de projet (init)\n**Étant donné** a\n**Quand** b\n**Alors** c\n\n"
            + "### REQ-PM02 : Arbitre (lint)\n**Étant donné** a\n**Quand** b\n**Alors** c\n\n"
            + "## Phases\n\n| Phase | Contenu | Jalon | Statut |\n|---|---|---|---|\n"
            + "| P1 | `sdd init` du projet | j1 | approuvée |\n| P2 | arbitre lint | j2 | à approuver |\n");
        var spec = SpecModel.Load(Path.Combine(dir, "docs", "specs", "SPEC-T.md"));
        Check(spec.PhaseMentions("`sdd init` du projet", "REQ-PM01"), "parenthèse du titre → matching");
        Check(!spec.PhaseMentions("`sdd init` du projet", "REQ-PM02"), "pas de fuite entre phases");
        Check(spec.PhaseMentions("arbitre SDD-L001..L008", "REQ-PM02"), "id ou mot-signature → matching");
        Check(!spec.PhaseMentions("phase sans rapport", "REQ-INCONNUE99"), "REQ absente → false, sans exception");
    }

    private static string[] Sample(string resource)
    {
        using Stream s = typeof(Tests).Assembly.GetManifestResourceStream(
            typeof(Tests).Assembly.GetManifestResourceNames().First(n => n.EndsWith(resource, StringComparison.Ordinal)))!;
        using var reader = new StreamReader(s);
        return reader.ReadToEnd().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
    }
}
