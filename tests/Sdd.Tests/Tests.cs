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
        Check(backlog.Contains("Toute idée pospuesta s'enregistre ici dans le commit qui la pose.", StringComparison.Ordinal),
              "BACKLOG : règle anti-oubli littérale");

        string state = Read(Path.Combine(dir, "docs", "AGENT_STATE.md"));
        Check(state.Contains("Lee docs/AGENT_STATE.md y continúa", StringComparison.Ordinal), "AGENT_STATE : règle de reprise");
        Check(state.Contains("## Spécifications en vol", StringComparison.Ordinal), "AGENT_STATE : section en vol");

        string toml = Read(Path.Combine(dir, "sdd.toml"));
        Check(toml.Contains("nom = \"portail-citoyen\"", StringComparison.Ordinal), "sdd.toml : nom du projet");
        Check(toml.Contains("version = \"1.0\"", StringComparison.Ordinal), "sdd.toml : doctrine pin v1.0");

        string wf = Read(Path.Combine(dir, ".github", "workflows", "sdd-lint.yml"));
        Check(wf.Contains("lint non implémenté — P2", StringComparison.Ordinal) && wf.Contains("exit 1", StringComparison.Ordinal),
              "workflow placeholder échouant « lint non implémenté — P2 »");
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
            Deco("EXEMPLE", "Brouillon", "- **D1** z — ouverte\n")
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
        Check(lines.Any(l => l.Contains("◐ 50 %", StringComparison.Ordinal)),
              "progression réelle : 2 phases approuvées / 4 → ◐ 50 % (« à approuver » ne compte pas)");
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
              "ligne D1 = texte owner + RATIFIÉE + date");
        Check(spec.Contains("**Version** : 1.1", StringComparison.Ordinal), "bump version mineure 1.0 → 1.1");
        Check(Regex.IsMatch(spec, @"- v1\.1 \(\d{2}/\d{2}/\d{4}\) : décision D1 ratifiée par l'owner"),
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

    private static string[] Sample(string resource)
    {
        using Stream s = typeof(Tests).Assembly.GetManifestResourceStream(
            typeof(Tests).Assembly.GetManifestResourceNames().First(n => n.EndsWith(resource, StringComparison.Ordinal)))!;
        using var reader = new StreamReader(s);
        return reader.ReadToEnd().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
    }
}
