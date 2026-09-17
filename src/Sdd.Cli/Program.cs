using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// SDD-Kit CLI v1 — doctrine + arbitre + trace (init, new, lint, status,
/// decide, trace, agent-brief, adopt).
/// Frontière dure : la CLI fournit squelette, arbitre et trace ;
/// elle n'écrit jamais de prose de spec. Zéro dépendance NuGet (BCL seul).
/// </summary>
public static class Program
{
    public const string DoctrineVersion = "1.0";

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string root = Directory.GetCurrentDirectory();
        return args[0] switch
        {
            "init" => Init(args[1..]),
            "adopt" => Adopt.Run(root, args[1..]),
            "new" => New(args[1..]),
            "lint" => Lint.Run(root, ci: args[1..].Contains("--ci")),
            "status" => Status.Run(root),
            "decide" => Decide.Run(root, args[1..]),
            "trace" => Trace.Run(root, args[1..]),
            "agent-brief" => Brief.Run(root, args[1..]),
            "--version" or "-v" => PrintVersion(),
            "help" or "--help" or "-h" => PrintUsage(),
            _ => UnknownCommand(args[0]),
        };
    }

    private static int UnknownCommand(string cmd)
    {
        Err($"commande inconnue : « {cmd} »");
        PrintUsage();
        return 1;
    }

    private static int PrintVersion()
    {
        string v = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "inconnue";
        Console.WriteLine($"sdd {v} — doctrine v{DoctrineVersion} embarquée");
        return 0;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            sdd — SDD-Kit CLI (v1 complète : P1–P4)

            Usage :
              sdd init --projet <nom>    initialise un projet SDD greenfield dans le cwd
              sdd adopt --projet <nom>   onramp brownfield : audit → BACKLOG, puis infrastructures SDD
              sdd new <FAMILLE>          crée docs/specs/SPEC-<FAMILLE>.md (Brouillon,
                                         placeholders [À RATIFIER], zéro prose inventée)
              sdd lint [--ci]            arbitre SDD-L001..L008 (--ci : sortie condensée + exit code CI)
              sdd status                 tableau ASCII normatif (Annexe A)
              sdd decide <SPEC> <Dn> "<texte>" --ratifiee
                                         ratifie une décision + Historique + commit atomique
              sdd trace <REQ>            commits, phases, tâches et statut global d'une REQ
              sdd agent-brief <SPEC> <Pn> --pour <agent>
                                         prompt auto-suffisant pour agent borné
              sdd --version              version installée
              sdd help                   ce texte

            Zéro dépendance NuGet ; la CLI n'écrit jamais de prose de spec.
            """);
        return 0;
    }

    // ---------- init ----------

    private static int Init(string[] args)
    {
        if (args.Length != 2 || args[0] != "--projet" || string.IsNullOrWhiteSpace(args[1]))
        {
            Err("usage : sdd init --projet <nom>");
            return 1;
        }

        string nom = args[1];
        if (!Regex.IsMatch(nom, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$"))
        {
            Err($"nom de projet invalide : « {nom} » (lettres, chiffres, point, tiret, underscore ; ≤ 64 caractères)");
            return 1;
        }

        string root = Directory.GetCurrentDirectory();
        var files = Scaffold.Build(root, nom);

        var conflicts = files.Keys.Where(File.Exists).ToList();
        if (conflicts.Count > 0)
        {
            Err("projet SDD déjà initialisé ou fichiers en collision :");
            foreach (var c in conflicts)
            {
                Console.Error.WriteLine($"  - {Relative(root, c)}");
            }
            Err("`sdd init` n'écrase jamais des fichiers existants (doctrine : tree propre, zéro surprise).");
            return 1;
        }

        foreach (var (path, content) in files)
        {
            Write(path, content);
            Console.WriteLine($"  créé  {Relative(root, path)}");
        }

        string specsDir = Path.Combine(root, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        Console.WriteLine($"  créé  {Relative(root, specsDir)}/");

        Console.WriteLine();
        Console.WriteLine($"Projet SDD « {nom} » initialisé (doctrine v{DoctrineVersion} pin).");
        Console.WriteLine("Premier réflexe : commit initial contenant tous ces artefacts, puis `sdd new <FAMILLE>`.");
        return 0;
    }

    // ---------- new ----------

    private static int New(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Err("usage : sdd new <FAMILLE>  (MAJUSCULES, chiffres, tirets — ex. PORTAIL-CITOYEN)");
            return 1;
        }

        string famille = args[0];
        if (!Regex.IsMatch(famille, @"^[A-Z][A-Z0-9]*(-[A-Z0-9]+)*$"))
        {
            Err($"FAMILLE invalide : « {famille} » (attendu : MAJUSCULES, chiffres et tirets, ex. VIZ-LIGNAGE)");
            return 1;
        }

        string root = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(root, "sdd.toml")))
        {
            Err("aucun projet SDD ici (sdd.toml absent) — exécutez d'abord `sdd init --projet <nom>`.");
            return 1;
        }

        string date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string target = Path.Combine(root, "docs", "specs", $"SPEC-{famille}.md");
        if (File.Exists(target))
        {
            Err($"{Relative(root, target)} existe déjà — la CLI n'écrase pas une spec en cours de ratification.");
            return 1;
        }

        string content = Resource("SPEC-template.md")
            .Replace("{{FAMILLE}}", famille)
            .Replace("{{DATE}}", date);

        Write(target, content);
        Console.WriteLine($"  créé  {Relative(root, target)}");

        string agentState = Path.Combine(root, "docs", "AGENT_STATE.md");
        if (RegisterInFlight(agentState, famille, date))
        {
            Console.WriteLine($"  mis à jour  {Relative(root, agentState)} (spécification en vol)");
        }

        Console.WriteLine();
        Console.WriteLine("Brouillon squelette : chaque [À RATIFIER] est du texte à écrire par l'humain, jamais par l'outil.");
        Console.WriteLine($"Prochaine étape : ratification owner, puis commit « docs({famille.ToLowerInvariant()}): ... ».");
        return 0;
    }

    private static bool RegisterInFlight(string agentStatePath, string famille, string date)
    {
        if (!File.Exists(agentStatePath))
        {
            return false;
        }

        string text = File.ReadAllText(agentStatePath);
        string entry = $"- SPEC-{famille}.md — Brouillon v1.0 ({date}) — [À RATIFIER]";
        if (text.Contains($"SPEC-{famille}.md — Brouillon"))
        {
            return false; // déjà consignée
        }

        const string section = "## Spécifications en vol";
        int idx = text.IndexOf(section, StringComparison.Ordinal);
        if (idx < 0)
        {
            File.WriteAllText(agentStatePath, text.TrimEnd() + "\n\n" + section + "\n\n" + entry + "\n");
            return true;
        }

        int markerPos = text.IndexOf(Scaffold.EnVolMarker, StringComparison.Ordinal);
        if (markerPos >= 0 && markerPos > idx)
        {
            text = text.Replace(Scaffold.EnVolMarker, entry);
        }
        else
        {
            int after = idx + section.Length;
            int nextSection = text.IndexOf("\n## ", after, StringComparison.Ordinal);
            int insertAt = nextSection < 0 ? text.Length : nextSection;
            string head = text[..insertAt].TrimEnd();
            string tail = nextSection < 0 ? "" : text[insertAt..];
            text = head + "\n\n" + entry + "\n\n" + tail.TrimStart('\n');
        }

        File.WriteAllText(agentStatePath, text);
        return true;
    }

    // ---------- communs ----------

    internal static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    public static string Resource(string suffix)
    {
        Assembly asm = typeof(Program).Assembly;
        string? name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));
        if (name is null)
        {
            throw new InvalidOperationException($"ressource embarquée introuvable : {suffix}");
        }

        using Stream stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        string text = reader.ReadToEnd();
        return text.Replace("\r\n", "\n");
    }

    internal static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static void Err(string msg) => Console.Error.WriteLine($"✖ {msg}");
}
