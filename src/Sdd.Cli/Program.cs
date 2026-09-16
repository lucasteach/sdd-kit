using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// SDD-Kit CLI — P1 : scaffold (`init`, `new`).
/// Frontière dure : la CLI fournit squelette, arbitre et trace ;
/// elle n'écrit jamais de prose de spec. Zéro dépendance NuGet (BCL seul).
/// </summary>
public static class Program
{
    public const string DoctrineVersion = "1.0";
    private const string EnVolMarker = "_(aucune — `sdd new` ajoute une entrée ici)_";

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        return args[0] switch
        {
            "init" => Init(args[1..]),
            "new" => New(args[1..]),
            "lint" => Lint.Run(Directory.GetCurrentDirectory()),
            "status" => Status.Run(Directory.GetCurrentDirectory()),
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

    private static int PrintUsage()
    {
        Console.WriteLine("""
            sdd — SDD-Kit CLI (P1 : scaffold)

            Usage :
              sdd init --projet <nom>     initialise un projet SDD dans le cwd
              sdd new <FAMILLE>           crée docs/specs/SPEC-<FAMILLE>.md (Brouillon,
                                          placeholders [À RATIFIER], zéro prose inventée)

            Implémentées en P2/P3 : lint, status, decide, trace, agent-brief, adopt.
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
        var files = new Dictionary<string, string>
        {
            [Path.Combine(root, "sdd.toml")] = BuildToml(nom),
            [Path.Combine(root, "docs", "DOCTRINE.md")] = Resource("DOCTRINE.md"),
            [Path.Combine(root, "docs", "AGENT_STATE.md")] = BuildAgentState(nom),
            [Path.Combine(root, "docs", "BACKLOG.md")] = BuildBacklog(nom),
            [Path.Combine(root, ".github", "workflows", "sdd-lint.yml")] = BuildWorkflowPlaceholder(),
        };

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

    private static string BuildToml(string nom) =>
        $"""
        # sdd.toml — métadonnées du projet (généré par `sdd init`)
        [projet]
        nom = "{nom}"

        [doctrine]
        version = "{DoctrineVersion}"  # pin : docs/DOCTRINE.md, embarqué verbatim par la CLI

        """;

    private static string BuildAgentState(string nom) =>
        $"""
        # AGENT_STATE — {nom}

        <!-- Mémoire de continuité inter-sessions. Source de vérité du repo. -->

        ## Convention de reprise

        Le **premier message** d'un nouveau chat sur ce projet doit être :

        > « Lee docs/AGENT_STATE.md y continúa »

        ## État courant

        | Champ | Valeur |
        |---|---|
        | Phase active | — |
        | Spec de référence | — |
        | Next action | — |

        ## Spécifications en vol

        {EnVolMarker}

        ## Table des commits de session

        <!-- Une ligne par session : hash, date, agent, résumé, prochaine action. -->

        | Hash | Date | Agent | Résumé | Prochaine action |
        |---|---|---|---|---|

        """;

    private static string BuildBacklog(string nom) =>
        $"""
        # BACKLOG — {nom}

        Registre des idées, dettes et demandes reportées du projet.

        ## Règle anti-oubli

        > **Toute idée pospuesta s'enregistre ici dans le commit qui la pose.**

        Quand une idée, amélioration ou correction est repoussée hors du périmètre
        de la tâche en cours, elle est ajoutée à ce fichier **dans le même commit**
        que celui qui la postpone. Aucune idée reportée ne vit uniquement dans une
        conversation.

        ## Entrées

        <!-- Format : - [ ] (YYYY-MM-DD, origine) description → phase/version visée -->

        _(vide)_

        """;

    private static string BuildWorkflowPlaceholder() =>
        """
        # CI gate SDD — placeholder P1, bloquant volontaire (REQ-CLI09, règles en P2)
        name: sdd-lint
        on:
          push:
          pull_request:
        jobs:
          lint:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@v4
              - name: sdd lint
                run: |
                  echo "lint non implémenté — P2" >&2
                  exit 1

        """;

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

        int markerPos = text.IndexOf(EnVolMarker, StringComparison.Ordinal);
        if (markerPos >= 0 && markerPos > idx)
        {
            text = text.Replace(EnVolMarker, entry);
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

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static string Resource(string suffix)
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

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static void Err(string msg) => Console.Error.WriteLine($"✖ {msg}");
}
