using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

public enum Sev { Error, Warning }

public sealed record Finding(Sev Severity, string Rule, string Title, List<string> Details);

/// <summary>
/// sdd lint — arbitre SDD-L001..L008 (REQ-CLI04 + langue D3). Sortie au format
/// normatif Annexe A. Erreur → exit 1 (bloque CI) sauf waiver justifie dans sdd.toml.
/// </summary>
public static class Lint
{
    /// <summary>
    /// Liste noire unique (micro-fix i18n 1.0.1) : termes non-FR herites des
    /// textes fondateurs spanglish. SDD-L008 les signale en warning dans les
    /// docs du projet audite ; le test de garde T27 l'utilise aussi.
    /// Pour etendre : ajouter le terme ici, un seul endroit.
    /// </summary>
    public static readonly string[] LangBlacklist = { "pospuesta", "hallazgo", "hallazgos", "owner" };

    private static readonly Regex SpecToken = new(@"SPEC-[A-Z][A-Z0-9-]*", RegexOptions.Compiled);
    private static readonly string[] CodeFences =
    {
        "csharp", "cs", "fsharp", "vb", "bash", "sh", "shell", "zsh", "powershell",
        "yaml", "yml", "json", "toml", "xml", "ini", "sql", "js", "ts", "python",
        "py", "java", "go", "rust", "html", "css", "dockerfile", "makefile",
    };

    public static int Run(string root, TextWriter? writer = null, bool ci = false)
    {
        writer ??= Console.Out;
        TomlLite.ProjectMeta meta = TomlLite.Load(root);
        var findings = new List<Finding>();
        int checks = 0;

        var docsFiles = CollectDocsFiles(root);
        var existingSpecs = new HashSet<string>(
            Directory.Exists(Path.Combine(root, "docs", "specs"))
                ? Directory.GetFiles(Path.Combine(root, "docs", "specs"), "SPEC-*.md").Select(f => Path.GetFileName(f)!)
                : Array.Empty<string>(),
            StringComparer.Ordinal);

        foreach ((string relPath, string[] lines) in docsFiles)
        {
            var fences = FencedRanges(lines);
            bool isSpec = relPath.StartsWith("specs/SPEC-", StringComparison.Ordinal);

            if (isSpec)
            {
                CheckReqGwt(relPath, lines, fences, findings, ref checks);
                CheckDecisions(relPath, lines, fences, findings, ref checks);
                CheckPhases(relPath, lines, fences, findings, ref checks);
                CheckHistorique(relPath, lines, findings, ref checks);
            }

            if (relPath.Equals("BACKLOG.md", StringComparison.Ordinal))
            {
                CheckBacklog(relPath, lines, fences, findings, ref checks);
            }

            CheckSpecRefs(relPath, lines, fences, existingSpecs, findings, ref checks);
            CheckMagicNumbers(relPath, lines, fences, findings, ref checks);
            CheckLanguage(relPath, lines, fences, findings, ref checks);
        }

        // waivers
        var waived = new List<Finding>();
        var kept = new List<Finding>();
        foreach (Finding f in findings)
        {
            if (meta.Waivers.Any(w => MatchWaiver(w, f)))
            {
                waived.Add(f);
            }
            else
            {
                kept.Add(f);
            }
        }

        int errors = kept.Count(f => f.Severity == Sev.Error);
        int warnings = kept.Count(f => f.Severity == Sev.Warning);
        int ok = Math.Max(checks - kept.Count, 0);

        if (ci)
        {
            bool gha = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            foreach (Finding f in kept.Where(f => f.Severity == Sev.Error)
                         .Concat(kept.Where(f => f.Severity == Sev.Warning)))
            {
                string sev = f.Severity == Sev.Error ? "ERREUR" : "AVERTISSEMENT";
                string joined = string.Join(" ", f.Details);
                writer.WriteLine($"{f.Rule} {sev} {joined}");
                if (gha)
                {
                    Match loc = Regex.Match(f.Details.Count > 0 ? f.Details[0] : "", @"^(\S+?):(\d+)");
                    string file = loc.Success ? "docs/" + loc.Groups[1].Value : "docs";
                    string ln = loc.Success ? loc.Groups[2].Value : "1";
                    writer.WriteLine($"::{(f.Severity == Sev.Error ? "error" : "warning")} file={file},line={ln},title={f.Rule}::{f.Title} — {joined}");
                }
            }

            writer.WriteLine($"RESUME regles_ok={ok} erreurs={errors} warnings={warnings} waivers={waived.Count}");
            return errors > 0 ? 1 : 0;
        }

        foreach (Finding f in kept.Where(f => f.Severity == Sev.Error))
        {
            EmitFinding(writer, '✖', f);
        }

        foreach (Finding f in kept.Where(f => f.Severity == Sev.Warning))
        {
            EmitFinding(writer, '⚠', f);
        }

        var summary = new StringBuilder($"✔ {ok} règles OK · {errors} erreurs");
        if (warnings > 0)
        {
            summary.Append($" · {warnings} warnings");
        }

        summary.Append($" · {waived.Count} waivers");
        writer.WriteLine(summary.ToString());

        return errors > 0 ? 1 : 0;
    }

    private static bool MatchWaiver(Waiver w, Finding f) =>
        string.Equals(w.Regle, f.Rule, StringComparison.OrdinalIgnoreCase)
        && (string.IsNullOrEmpty(w.Portee)
            || f.Details.Count > 0 && f.Details[0].StartsWith(w.Portee.Replace("docs/", ""), StringComparison.Ordinal));

    private static void EmitFinding(TextWriter w, char mark, Finding f)
    {
        if (f.Rule == "SDD-L002" && f.Details.Count == 1)
        {
            w.WriteLine($"{mark} {f.Rule}  {f.Title} : {f.Details[0]}");
            return;
        }

        w.WriteLine($"{mark} {f.Rule}  {f.Title}");
        foreach (string d in f.Details)
        {
            w.WriteLine($"            {d}");
        }
    }

    // ---------- collecte ----------

    private static List<(string Rel, string[] Lines)> CollectDocsFiles(string root)
    {
        var result = new List<(string Rel, string[] Lines)>();
        string docs = Path.Combine(root, "docs");
        if (!Directory.Exists(docs))
        {
            return result;
        }

        foreach (string file in Directory.GetFiles(docs, "*.md")
                     .Concat(Directory.Exists(Path.Combine(docs, "specs"))
                         ? Directory.GetFiles(Path.Combine(docs, "specs"), "SPEC-*.md")
                         : Array.Empty<string>()))
        {
            if (!File.Exists(file))
            {
                continue;
            }

            string rel = Path.GetRelativePath(docs, file).Replace('\\', '/');
            if (!result.Any(r => r.Rel == rel))
            {
                result.Add((rel, File.ReadAllLines(file)));
            }
        }

        return result.OrderBy(r => r.Rel, StringComparer.Ordinal).ToList();
    }

    private static bool[] FencedRanges(string[] lines)
    {
        var inside = new bool[lines.Length];
        bool open = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inside[i] = true; // la ligne du fence elle-même ne compte pas comme contenu
                open = !open;
                continue;
            }

            inside[i] = open;
        }

        return inside;
    }

    // ---------- règles ----------

    private static void CheckReqGwt(string rel, string[] lines, bool[] fences, List<Finding> findings, ref int checks)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (fences[i] || !lines[i].StartsWith("### REQ-", StringComparison.Ordinal))
            {
                continue;
            }

            int end = i + 1;
            while (end < lines.Length && !lines[end].StartsWith("###", StringComparison.Ordinal)
                   && !lines[end].StartsWith("## ", StringComparison.Ordinal))
            {
                end++;
            }

            string block = string.Join('\n', lines[i..end]);
            checks++;
            var missing = new List<string>();
            if (!block.Contains("**Étant donné**", StringComparison.Ordinal)) missing.Add("Étant donné");
            if (!block.Contains("**Quand**", StringComparison.Ordinal)) missing.Add("Quand");
            if (!block.Contains("**Alors**", StringComparison.Ordinal)) missing.Add("Alors");
            if (missing.Count > 0)
            {
                string reqId = lines[i].Substring(4).Split(' ')[0];
                findings.Add(new Finding(Sev.Error, "SDD-L001", "REQ sans Given/When/Then",
                    [$"{rel}:{i + 1} {reqId} — manque « {string.Join(", ", missing)} »"]));
            }
        }
    }

    private static void CheckDecisions(string rel, string[] lines, bool[] fences, List<Finding> findings, ref int checks)
    {
        int start = Section(lines, fences, "## Décisions");
        if (start < 0)
        {
            return;
        }

        string specName = Path.GetFileNameWithoutExtension(rel);
        for (int i = start; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++)
        {
            if (fences[i] || !Regex.IsMatch(lines[i], @"^-\s+\*\*D\d+\*\*"))
            {
                continue;
            }

            checks++;
            string[] statutWords = { "ratifiée", "ratifiee", "différée", "différee", "ouverte", "à ratifier", "en attente", "approuvée" };
            bool hasStatus = statutWords.Any(word => lines[i].Contains(word, StringComparison.OrdinalIgnoreCase));
            if (!hasStatus)
            {
                string d = Regex.Match(lines[i], @"\*\*(D\d+)\*\*").Groups[1].Value;
                findings.Add(new Finding(Sev.Error, "SDD-L002", "décision sans statut",
                    [$"{d} ({specName})"]));
            }
        }
    }

    private static void CheckPhases(string rel, string[] lines, bool[] fences, List<Finding> findings, ref int checks)
    {
        int start = Section(lines, fences, "## Phases");
        if (start < 0)
        {
            start = Section(lines, fences, "## Phases d'implémentation");
        }

        if (start < 0)
        {
            return;
        }

        for (int i = start; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++)
        {
            if (fences[i] || !Regex.IsMatch(lines[i], @"^\|\s*P\d+\s*\|"))
            {
                continue;
            }

            checks++;
            string[] cells = lines[i].Split('|').Select(c => c.Trim()).ToArray();
            // cells[0] vide (avant le 1er |) ; Phase | Contenu | Jalon | Statut
            string jalon = cells.Length > 3 ? cells[3] : "";
            if (jalon.Length == 0 || jalon == "—")
            {
                string phase = cells.Length > 1 ? cells[1] : "?";
                findings.Add(new Finding(Sev.Error, "SDD-L005", "phase sans jalon visible",
                    [$"{rel}:{i + 1} {phase} — jalon vide dans le tableau des phases"]));
            }
        }
    }

    private static void CheckBacklog(string rel, string[] lines, bool[] fences, List<Finding> findings, ref int checks)
    {
        int start = Section(lines, fences, "## Entrées");
        if (start < 0)
        {
            return;
        }

        for (int i = start; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++)
        {
            if (fences[i] || !(lines[i].StartsWith("- [ ]", StringComparison.Ordinal) || lines[i].StartsWith("- [x]", StringComparison.Ordinal)))
            {
                continue;
            }

            checks++;
            // l'entrée peut se poursuivre sur les lignes suivantes (indentées)
            int j = i;
            while (j + 1 < lines.Length && lines[j + 1].StartsWith("  ", StringComparison.Ordinal)
                   && !lines[j + 1].StartsWith("## ", StringComparison.Ordinal))
            {
                j++;
            }

            string entry = string.Join('\n', lines[i..(j + 1)]);
            bool hasOrigin = entry.Contains("origine", StringComparison.OrdinalIgnoreCase)
                             || Regex.IsMatch(entry, @"\d{4}-\d{2}-\d{2}");
            bool hasStatus = lines[i].StartsWith("- [x]", StringComparison.Ordinal)
                             || entry.Contains("clos", StringComparison.OrdinalIgnoreCase)
                             || entry.Contains("ouvert", StringComparison.OrdinalIgnoreCase)
                             || entry.Contains("en attente", StringComparison.OrdinalIgnoreCase);
            if (!hasOrigin && !hasStatus)
            {
                string id = Regex.Match(entry, @"[A-Z]+-\d+").Value;
                findings.Add(new Finding(Sev.Error, "SDD-L004", "entrée BACKLOG sans origine ni statut",
                    [$"{rel}:{i + 1} {id} — ajouter (YYYY-MM-DD, origine : …) et statut ouvert/clos"]));
            }
        }
    }

    private static void CheckHistorique(string rel, string[] lines, List<Finding> findings, ref int checks)
    {
        checks++;
        if (!lines.Any(l => l.StartsWith("## Historique", StringComparison.Ordinal)))
        {
            findings.Add(new Finding(Sev.Warning, "SDD-L007", "spécification sans Historique",
                [$"{rel}:1 ajouter « ## Historique » avec v1.0 (date) : Brouillon initial"]));
        }
    }

    private static void CheckSpecRefs(string rel, string[] lines, bool[] fences, HashSet<string> existingSpecs,
        List<Finding> findings, ref int checks)
    {
        string selfName = Path.GetFileNameWithoutExtension(rel) + ".md";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Length; i++)
        {
            if (fences[i])
            {
                continue;
            }

            foreach (Match m in SpecToken.Matches(lines[i]))
            {
                string token = m.Value;
                checks++;
                string fileName = token + ".md";
                if (fileName == selfName || existingSpecs.Contains(fileName) || !seen.Add(token))
                {
                    continue;
                }

                findings.Add(new Finding(Sev.Error, "SDD-L003", "référence fantasma",
                    [$"{rel}:{i + 1} cite {fileName} —", "absente de docs/specs/"]));
            }
        }
    }

    private static void CheckMagicNumbers(string rel, string[] lines, bool[] fences, List<Finding> findings, ref int checks)
    {
        int i = 0;
        while (i < lines.Length)
        {
            string trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            string lang = trimmed.Substring(3).Trim().ToLowerInvariant();
            int start = i + 1;
            i++;
            while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                i++;
            }

            checks++;
            if (!CodeFences.Contains(lang) || lang.Length == 0)
            {
                continue; // bloc non-code (ex. ASCII normatif) : hors périmètre L006
            }

            var magic = new List<string>();
            for (int k = start; k < i && k < lines.Length; k++)
            {
                foreach (Match m in Regex.Matches(lines[k], @"(?<![\w.-])\d{3,}(?![\w-])"))
                {
                    string n = m.Value;
                    if (n.Length == 4 && long.TryParse(n, out long year) && year is >= 1900 and <= 2099)
                    {
                        continue; // années tolérées
                    }

                    magic.Add($"{n} (ligne {k + 1})");
                }
            }

            if (magic.Count > 0)
            {
                findings.Add(new Finding(Sev.Warning, "SDD-L006", "magic numbers dans snippets code",
                    [$"{rel} [{lang}] : {string.Join(", ", magic.Distinct().Take(6))}"]));
            }
        }
    }

    private static void CheckLanguage(string rel, string[] lines, bool[] fences, List<Finding> findings, ref int checks)
    {
        checks++;
        foreach (string term in LangBlacklist)
        {
            Regex rx = new($@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!fences[i] && rx.IsMatch(lines[i]))
                {
                    findings.Add(new Finding(Sev.Warning, "SDD-L008", "langue non conforme au pin D3",
                        [$"{rel}:{i + 1} terme « {term} » — FR par défaut (D3) : remplacer par l'équivalent français"]));
                    break; // une occurrence par fichier et par terme
                }
            }
        }
    }

    private static int Section(string[] lines, bool[] fences, string header)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (!fences[i] && lines[i].StartsWith(header, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return -1;
    }
}
