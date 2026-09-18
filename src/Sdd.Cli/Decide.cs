using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// sdd decide — REQ-CLI06 : ratifie une decision (statut + date), append
/// au Historique (bump version mineure) et genere un commit atomique.
/// Le texte de la decision vient du responsable ; la CLI n'invente jamais de prose.
/// </summary>
public static class Decide
{
    public static int Run(string root, string[] args, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        var positional = new List<string>();
        bool ratifiee = false;
        bool force = false;
        foreach (string a in args)
        {
            if (a == "--ratifiee")
            {
                ratifiee = true;
            }
            else if (a == "--force")
            {
                force = true;
            }
            else
            {
                positional.Add(a);
            }
        }

        if (positional.Count != 3 || !ratifiee)
        {
            writer.WriteLine("usage : sdd decide <SPEC> <Dn> \"<texte>\" --ratifiee [--force]");
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

        // 0b) validation structurelle AVANT toute écriture (micro-fix audit externe) :
        //     une spec héritée sans Version ni Historique produisait des bumps « v »
        //     fantômes et des insertions à l'aveugle (document corrompu en silence).
        var manquants = new List<string>();
        if (spec.FindLine("**Version**") < 0 || spec.Version().Length == 0)
        {
            manquants.Add("la ligne « **Version** : X.Y »");
        }

        if (spec.FindLine("## Historique") < 0)
        {
            manquants.Add("la section « ## Historique »");
        }

        if (manquants.Count > 0)
        {
            writer.WriteLine($"✖ {spec.Name}.md est incomplet pour `sdd decide` : il manque {string.Join(" et ", manquants)}.");
            writer.WriteLine("  document INTACT (aucune écriture) — ajoutez les sections, puis relancez.");
            return 1;
        }

        // précondition git APRÈS les validations spécifiques (message d'erreur le plus
        // précis d'abord) ; indispensable pour la garantie de commit atomique.
        if (!Git.IsRepo(root))
        {
            writer.WriteLine("✖ `sdd decide` exige un dépôt git (commit atomique garanti) — `git init` d'abord.");
            return 1;
        }

        // 0) idempotence (micro-fix 1.0.2) : decision deja ratifiee avec le MEME texte
        //    -> no-op sans bump, sans entree Historique, sans commit (--force re-edite).
        string body = texte.Trim();
        Match cur = Regex.Match(decision.Value.Text, @"^- \*\*" + dId + @"\*\*\s*(.*?)\s+—\s*(.*)$");
        bool alreadyRatified = cur.Success
            && cur.Groups[2].Value.Contains("RATIFIÉE", StringComparison.OrdinalIgnoreCase);
        if (alreadyRatified && cur.Groups[1].Value == body && !force)
        {
            writer.WriteLine($"  no-op : {spec.Name} {dId} déjà ratifiée avec ce texte — aucune entrée Historique, aucun commit.");
            writer.WriteLine("  (utiliser --force pour rééditer, ou changer le texte pour un nouveau bump)");
            return 0;
        }

        // 1) mise a jour de la ligne de decision (corps = texte du responsable, statut = ratifiee + date)
        string now = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string shortDate = DateTime.Now.ToString("dd/MM", CultureInfo.InvariantCulture);
        string newLine = $"- **{dId}** {body} — **RATIFIÉE {shortDate}**";
        spec.Lines[decision.Value.Index] = newLine;

        // 2) bump version mineure + append Historique (sections garanties par la validation 0b)
        string oldVer = spec.Version();
        string[] partsV = oldVer.Split('.');
        string newVer = $"{partsV[0]}.{int.Parse(partsV[1], CultureInfo.InvariantCulture) + 1}";
        int vIdx = spec.FindLine("**Version**");
        spec.Lines[vIdx] = Regex.Replace(spec.Lines[vIdx], @"\d+\.\d+", newVer);

        string histEntry = $"- v{newVer} ({now}) : décision {dId} ratifiée par le responsable";
        {
            int histLine = spec.FindLine("## Historique");
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

        var commit = Git.Run(root, "commit", "--only", "-m", subject, "-m", $"Décision du responsable : {body}", "--", rel);
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
