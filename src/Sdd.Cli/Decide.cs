using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// sdd decide — REQ-CLI06 : ratifie une decision (statut + date), append
/// au Historique (bump version mineure) et genere un commit atomique.
/// Le texte de la decision vient de l'owner ; la CLI n'invente jamais de prose.
/// </summary>
public static class Decide
{
    public static int Run(string root, string[] args, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        var positional = new List<string>();
        bool ratifiee = false;
        foreach (string a in args)
        {
            if (a == "--ratifiee")
            {
                ratifiee = true;
            }
            else
            {
                positional.Add(a);
            }
        }

        if (positional.Count != 3 || !ratifiee)
        {
            writer.WriteLine("usage : sdd decide <SPEC> <Dn> \"<texte>\" --ratifiee");
            return 1;
        }

        string specArg = positional[0];
        string dId = positional[1];
        string texte = positional[2];

        if (!Regex.IsMatch(dId, @"^D\d+$", RegexOptions.IgnoreCase))
        {
            writer.WriteLine($"✖ identifiant de décision invalide : « {dId} » (attendu D1, D2, …)");
            return 1;
        }

        dId = dId.ToUpperInvariant();

        string? specPath = SpecModel.ResolvePath(root, specArg);
        if (specPath is null)
        {
            writer.WriteLine($"✖ spec introuvable : docs/specs/{specArg} (précisez le nom exact, ex. SPEC-OUTIL-SDD)");
            return 1;
        }

        if (!Git.IsRepo(root))
        {
            writer.WriteLine("✖ `sdd decide` exige un dépôt git (commit atomique garanti) — `git init` d'abord.");
            return 1;
        }

        var spec = SpecModel.Load(specPath);
        (int Index, string Text)? decision = null;
        foreach ((int idx, string text) in spec.DecisionLines())
        {
            if (text.StartsWith($"- **{dId}**", StringComparison.Ordinal))
            {
                decision = (idx, text);
                break;
            }
        }

        if (decision is null)
        {
            writer.WriteLine($"✖ {dId} introuvable dans la section « ## Décisions » de {spec.Name}.md");
            return 1;
        }

        // 1) mise a jour de la ligne de decision (corps = texte owner, statut = ratifiee + date)
        string now = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string shortDate = DateTime.Now.ToString("dd/MM", CultureInfo.InvariantCulture);
        string body = texte.Trim();
        string newLine = $"- **{dId}** {body} — **RATIFIÉE {shortDate}**";
        spec.Lines[decision.Value.Index] = newLine;

        // 2) bump version mineure + append Historique
        string oldVer = spec.Version();
        string newVer = "";
        int histLine = spec.FindLine("## Historique");
        if (oldVer.Length > 0)
        {
            string[] parts = oldVer.Split('.');
            newVer = $"{parts[0]}.{int.Parse(parts[1], CultureInfo.InvariantCulture) + 1}";
            int vIdx = spec.FindLine("**Version**");
            spec.Lines[vIdx] = Regex.Replace(spec.Lines[vIdx], @"\d+\.\d+", newVer);
        }

        string histEntry = $"- v{(newVer.Length > 0 ? newVer : "x.y")} ({now}) : décision {dId} ratifiée par l'owner";
        if (histLine >= 0)
        {
            int end = spec.SectionEnd(histLine + 1);
            int last = histLine;
            for (int i = histLine + 1; i < end; i++)
            {
                if (spec.Lines[i].StartsWith("- v", StringComparison.Ordinal))
                {
                    last = i;
                }
            }

            spec.Lines.Insert(last + 1, histEntry);
        }
        else
        {
            int insertAt = spec.FindLine("**Responsable**") + 1;
            insertAt = insertAt > 0 ? insertAt : Math.Min(1, spec.Lines.Count);
            spec.Lines.InsertRange(insertAt, new[] { "", "## Historique", histEntry });
        }

        spec.Save();

        // 3) commit atomique (les deux changements vivent dans le meme fichier)
        string rel = Path.Combine("docs", "specs", $"{spec.Name}.md").Replace('\\', '/');
        string scope = spec.Name.StartsWith("SPEC-", StringComparison.Ordinal)
            ? spec.Name["SPEC-".Length..].ToLowerInvariant()
            : spec.Name.ToLowerInvariant();
        string subject = $"docs({scope}): ratification {dId} — v{newVer}";
        var add = Git.Run(root, "add", rel);
        if (add.exit != 0)
        {
            writer.WriteLine($"✖ git add a échoué ({add.stderr.Trim()}) — corrigez puis commitez à la main.");
            return 1;
        }

        var commit = Git.Run(root, "commit", "--only", "-m", subject, "-m", $"Décision owner : {body}", "--", rel);
        if (commit.exit != 0)
        {
            writer.WriteLine($"✖ git commit a échoué :\n{commit.stderr.Trim()}");
            writer.WriteLine("  (les fichiers sont déjà modifiés ; commitez à la main ou annulez avec git checkout --)");
            return 1;
        }

        string hash = Git.Run(root, "rev-parse", "--short", "HEAD").stdout.Trim();
        writer.WriteLine($"  décision {spec.Name} {dId} → RATIFIÉE {shortDate}");
        writer.WriteLine($"  Historique : {histEntry.TrimStart("- ".ToCharArray())}");
        writer.WriteLine($"  commit     : {hash}  {subject}");
        return 0;
    }
}
