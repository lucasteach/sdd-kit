using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// sdd trace — REQ-CLI07 : commits mentionnant la REQ (convention commit-msg),
/// phases liees, taches bornees associees, statut global.
/// Statut v1 (heuristique documentee) : sans commits → non demarree ;
/// phase associee approuvee et REQ sans [À RATIFIER] → implantee ; sinon partielle.
/// </summary>
public static class Trace
{
    public static int Run(string root, string[] args, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        if (args.Length != 1 || !Regex.IsMatch(args[0], @"^REQ-[A-Z0-9-]+\d+$", RegexOptions.IgnoreCase))
        {
            writer.WriteLine("usage : sdd trace <REQ-…>   (ex. sdd trace REQ-CLI07)");
            return 1;
        }

        string req = args[0].ToUpperInvariant();

        SpecModel? spec = null;
        string specsDir = Path.Combine(root, "docs", "specs");
        if (Directory.Exists(specsDir))
        {
            foreach (string f in Directory.GetFiles(specsDir, "SPEC-*.md"))
            {
                var candidate = SpecModel.Load(f);
                if (candidate.ReqBlocks().Any(b => b.Id == req))
                {
                    spec = candidate;
                    break;
                }
            }
        }

        if (spec is null)
        {
            writer.WriteLine($"✖ {req} n'apparaît comme cabecera « ### {req} » dans aucune spec de docs/specs/.");
            writer.WriteLine("  (sdd trace suit les REQ ratifiees ; une REQ sans spec reste invisible, par design)");
            return 1;
        }

        var commits = new List<string>();
        if (Git.IsRepo(root))
        {
            string log = Git.Run(root, "log", "--all", $"--grep={req}", "--date=short",
                "--format=%h %ad %s").stdout;
            commits = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var phases = spec.PhaseRows().Where(r => PhaseMentions(r.Contenu, req, spec, r.Phase)).ToList();
        var reqRange = spec.ReqBlocks().First(b => b.Id == req);
        string reqBody = string.Join('\n', spec.Lines[reqRange.Start..reqRange.End]);
        bool aRatifier = reqBody.Contains("[À RATIFIER]", StringComparison.Ordinal);

        writer.WriteLine($"{req} — {spec.Name}.md (v{spec.Version()})");
        writer.WriteLine($"Commits mentionnant la REQ : {commits.Count}");
        foreach (string c in commits)
        {
            writer.WriteLine($"  {c}");
        }

        writer.WriteLine("Phases concernées :");
        if (phases.Count > 0)
        {
            foreach (var p in phases)
            {
                writer.WriteLine($"  {p.Phase} — {p.Contenu} [{p.Statut}]");
            }
        }
        else
        {
            writer.WriteLine("  — (aucune ligne du tableau des phases ne nomme cette REQ ; convention `type(scope): Pn …` recommandée)");
        }

        writer.WriteLine("Tâches bornées associées :");
        string agentStatePath = Path.Combine(root, "docs", "AGENT_STATE.md");
        int listed = 0;
        if (File.Exists(agentStatePath))
        {
            bool inTasks = false;
            foreach (string line in File.ReadAllLines(agentStatePath))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inTasks = line.StartsWith("## Tâches bornées", StringComparison.Ordinal);
                    continue;
                }

                if (inTasks && line.StartsWith("- ", StringComparison.Ordinal) && line.Contains(req, StringComparison.Ordinal))
                {
                    writer.WriteLine($"  {line}");
                    listed++;
                }
            }
        }

        if (listed == 0)
        {
            writer.WriteLine("  — (aucune entrée de AGENT_STATE § « Tâches bornées » ne cite la REQ)");
        }

        string statut = commits.Count == 0 ? "non démarrée"
            : phases.Count > 0 && phases.All(p => p.Statut.Contains("approuvée", StringComparison.OrdinalIgnoreCase)) && !aRatifier
                ? "implémentée"
                : "partielle";
        writer.WriteLine($"Statut global : {statut}");
        return 0;
    }

    /// <summary>Lie une REQ a sa phase : id litteral, ou mot-cle du titre (parenthese ou mots > 4 lettres) present dans le Contenu.</summary>
    private static bool PhaseMentions(string contenu, string req, SpecModel spec, string phase)
    {
        string c = contenu.ToLowerInvariant().Replace("`", "");
        if (c.Contains(req.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return true;
        }

        var range = spec.ReqBlocks().First(b => b.Id == req);
        string title = spec.Lines[range.Start];
        string tail = title[(title.IndexOf(':') + 1)..];
        var paren = Regex.Match(tail, @"\(([^)]+)\)");
        if (paren.Success && c.Contains(paren.Groups[1].Value.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return true;
        }

        var words = Regex.Matches(tail.ToLowerInvariant(), @"[a-zéûîôàûè]{5,}");
        return words.Where(w => w.Value is not ("règles" or "spec" or "projet"))
            .Any(w => c.Contains(w.Value, StringComparison.Ordinal));
    }
}
