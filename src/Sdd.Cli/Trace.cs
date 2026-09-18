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
            writer.WriteLine($"✖ {req} n'apparaît comme en-tête « ### {req} » dans aucune spec de docs/specs/.");
            writer.WriteLine("  (sdd trace suit les REQ ratifiées ; une REQ sans spec reste invisible, par design)");
            return 1;
        }

        var commits = new List<string>();
        if (Git.IsRepo(root))
        {
            string log = Git.Run(root, "log", "--all", $"--grep={req}", "--date=short",
                "--format=%h %ad %s").stdout;
            commits = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var phases = spec.PhaseRows().Where(r => spec.PhaseMentions(r.Contenu, req)).ToList();
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

}
