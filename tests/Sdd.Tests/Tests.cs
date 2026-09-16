using System.Text;

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
}
