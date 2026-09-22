using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// sdd status — tableau ASCII normatif (Annexe A). Largeur interieure 62 ;
/// doctrine alignee en colonne 39 ; compteurs a droite en colonnes 18-19 ;
/// progression finissant en colonne 57. Test byte-identique sur fixture.
/// </summary>
public static class Status
{
    private const int Inner = 62;
    private const int DoctrineCol = 39;
    private const int ProgEndCol = 58; // exclusif (le « % » finit en col 57)

    public static int Run(string root, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        TomlLite.ProjectMeta meta = TomlLite.Load(root);

        string project = meta.Nom.Length > 0 ? meta.Nom : "(sans sdd.toml)";
        string doctrine = meta.Doctrine.Length > 0 ? meta.Doctrine : "?";

        string agentStatePath = Path.Combine(root, "docs", "AGENT_STATE.md");
        string[] agentState = File.Exists(agentStatePath) ? File.ReadAllLines(agentStatePath) : Array.Empty<string>();

        var specs = new List<SpecInfo>();
        string specsDir = Path.Combine(root, "docs", "specs");
        if (Directory.Exists(specsDir))
        {
            foreach (string f in Directory.GetFiles(specsDir, "SPEC-*.md").OrderBy(x => x, StringComparer.Ordinal))
            {
                specs.Add(SpecInfo.Load(f));
            }
        }

        // micro-fix audit II : « en vol » UNIQUEMENT depuis AGENT_STATE (aucune
        // heuristique de nom — tout fichier s'appelle SPEC-… — ni P1 forcé).
        string enVolSpec = CellValue(agentState, "Spec de référence", "Reference spec");
        string phaseActive = CellValue(agentState, "Phase active", "Active phase");
        string enVolToken = SpecToken(enVolSpec) ?? "";
        string enVolName = specs.FirstOrDefault(s => s.Name == enVolToken)?.Name ?? "";
        string phaseId = Regex.Match(phaseActive, @"P\d+").Value;

        // compteurs : dérivés SEULEMENT du **Statut** réel de chaque spec (FR ou EN)
        int brouillon = 0, approuvees = 0, enPhase = 0;
        foreach (SpecInfo s in specs)
        {
            if (s.Statut.Contains("en phase", StringComparison.OrdinalIgnoreCase)
                || s.Statut.Contains("in flight", StringComparison.OrdinalIgnoreCase)
                || s.Statut.Contains("in phase", StringComparison.OrdinalIgnoreCase))
            {
                enPhase++;
            }
            else if (s.Statut.Contains("approuv", StringComparison.OrdinalIgnoreCase)
                     || s.Statut.Contains("approved", StringComparison.OrdinalIgnoreCase))
            {
                approuvees++;
            }
            else
            {
                brouillon++;
            }
        }

        int decTotal = 0, decRat = 0, decDiff = 0, decOuv = 0, decOuvAnciennes = 0;
        foreach (SpecInfo s in specs)
        {
            foreach (string line in s.DecisionLines)
            {
                decTotal++;
                if (line.Contains("ratifiée", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("approuvée", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("ratified", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("approved", StringComparison.OrdinalIgnoreCase))
                {
                    decRat++;
                }
                else if (line.Contains("différée", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("deferred", StringComparison.OrdinalIgnoreCase))
                {
                    decDiff++;
                }
                else
                {
                    decOuv++;
                    Match dm = Regex.Match(line, @"(\d{4})-(\d{2})-(\d{2})");
                    if (dm.Success && DateTime.TryParseExact(dm.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime d) && (DateTime.Now - d).TotalDays > 14)
                    {
                        decOuvAnciennes++;
                    }
                }
            }
        }

        int backTotal = 0, backOuvert = 0, backClos = 0;
        string backlogPath = Path.Combine(root, "docs", "BACKLOG.md");
        if (File.Exists(backlogPath))
        {
            foreach (string line in File.ReadAllLines(backlogPath))
            {
                if (line.StartsWith("- [ ]", StringComparison.Ordinal)) { backTotal++; backOuvert++; }
                else if (line.StartsWith("- [x]", StringComparison.Ordinal)) { backTotal++; backClos++; }
            }
        }

        int taskTotal = 0, taskVertes = 0, taskRouges = 0;
        bool inTasks = false;
        foreach (string line in agentState)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inTasks = line.StartsWith("## Tâches bornées", StringComparison.Ordinal)
                          || line.StartsWith("## Bounded tasks", StringComparison.Ordinal);
                continue;
            }

            if (!inTasks)
            {
                continue;
            }

            if (line.StartsWith("- [x]", StringComparison.Ordinal)) { taskTotal++; taskVertes++; }
            else if (line.StartsWith("- [ ]", StringComparison.Ordinal)) { taskTotal++; taskRouges++; }
        }

        SpecInfo? vol = enVolName.Length > 0 ? specs.FirstOrDefault(s => s.Name == enVolName) : null;
        string phaseLabel = vol?.PhaseContenu(phaseId) ?? "";
        string jalon = vol is not null && phaseId.Length > 0 ? vol.NextJalon(phaseId) : "—";

        int pct = ComputeProgress(agentState, vol, phaseId);

        // alertes
        var alerts = new List<string>();
        int lintErrors = CountLintErrors(root);
        if (lintErrors > 0)
        {
            alerts.Add($"lint : {lintErrors} erreur(s) — voir `sdd lint`");
        }

        if (decOuvAnciennes > 0)
        {
            alerts.Add($"décision(s) ouverte(s) ancienne(s) : {decOuvAnciennes} — ratifier ou dater");
        }

        // rendu
        var o = writer;
        o.WriteLine('┌' + new string('─', Inner) + '┐');
        o.WriteLine(Row(HeaderRow(project, doctrine)));
        o.WriteLine('├' + new string('─', Inner) + '┤');
        o.WriteLine(Row(CountRow("SPECS", specs.Count, $"Brouillon {brouillon} · Approuvée {approuvees} · En phase {enPhase}")));
        o.WriteLine(Row(CountRow("DÉCISIONS", decTotal, $"ratifiée {decRat} · différée {decDiff} · ouverte {decOuv}")));
        o.WriteLine(Row(CountRow("BACKLOG", backTotal, $"ouvert {backOuvert} · clos {backClos}")));
        o.WriteLine(Row(CountRow("TÂCHES BORNÉES", taskTotal, $"vertes {taskVertes} · rouges {taskRouges}")));
        o.WriteLine('├' + new string('─', Inner) + '┤');
        o.WriteLine(Row(EnVolRow(enVolName.Length > 0 ? $"{enVolName} · {(phaseId.Length > 0 ? phaseId + " " : "")}({phaseLabel})" : "—", enVolName.Length > 0 ? pct : -1)));
        o.WriteLine(Row(LabelRow("JALON", jalon)));
        if (alerts.Count > 0)
        {
            o.WriteLine('├' + new string('─', Inner) + '┤');
            foreach (string a in alerts)
            {
                o.WriteLine(Row(LabelRow("ALERTES", a)));
            }
        }

        o.WriteLine('└' + new string('─', Inner) + '┘');
        return 0;
    }

    // ---------- mise en forme normative ----------

    private static string Row(string inner)
    {
        if (inner.Length > Inner)
        {
            inner = inner[..(Inner - 1)] + "…";
        }

        return '│' + inner.PadRight(Inner) + '│';
    }

    private static string HeaderRow(string project, string doctrine)
    {
        string label = "  SDD-Kit · " + project;
        string doctrineText = $"doctrine v{doctrine} (pin)";
        int col = Math.Max(DoctrineCol, label.Length + 1);
        return label + new string(' ', col - label.Length) + doctrineText;
    }

    private static string CountRow(string label, int num, string details) =>
        $"  {label,-16}{num,2}   {details}";

    private static string LabelRow(string label, string content) =>
        "  " + label.PadRight(9) + content;

    private static string EnVolRow(string content, int pct)
    {
        string prefix = "  " + "EN VOL".PadRight(9);
        if (pct < 0)
        {
            return prefix + content; // pas de spec en vol : « — » sans progression
        }

        string prog = $"◐ {pct} %";
        int room = ProgEndCol - prog.Length; // la progression se termine a ProgEndCol (exclusif)
        int contentStart = prefix.Length;
        if (contentStart + content.Length + 2 + prog.Length <= ProgEndCol)
        {
            string left = prefix + content;
            return left.PadRight(room) + prog;
        }

        int maxContent = room - contentStart - 2;
        if (maxContent < 1)
        {
            maxContent = 1;
        }

        content = content.Length > maxContent ? content[..maxContent] + "…" : content;
        return (prefix + content).PadRight(room) + prog;
    }

    // ---------- donnees ----------

    private static int ComputeProgress(string[] agentState, SpecInfo? vol, string phaseId)
    {
        string progCell = CellValue(agentState, "Progression", "Progress");
        Match m = Regex.Match(progCell, @"(\d+)\s*%");
        if (m.Success)
        {
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        if (vol is not null && vol.PhaseRows.Count > 0)
        {
            int done = vol.PhaseRows.Count(r => r.Statut.Contains("approuvée", StringComparison.OrdinalIgnoreCase)
                                               || r.Statut.Contains("approuvee", StringComparison.OrdinalIgnoreCase)
                                               || r.Statut.Contains("approved", StringComparison.OrdinalIgnoreCase));
            return (int)Math.Round(100.0 * done / vol.PhaseRows.Count);
        }

        return 0;
    }

    private static string CellValue(string[] lines, params string[] fields)
    {
        foreach (string line in lines)
        {
            if (!line.TrimStart().StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = line.Split('|').Select(c => c.Trim()).ToArray();
            for (int i = 1; i + 1 < cells.Length; i++)
            {
                string key = cells[i].Replace("*", "");
                if (fields.Any(f => key == f))
                {
                    return cells[i + 1];
                }
            }
        }

        return "";
    }

    private static string? SpecToken(string text) =>
        Regex.Match(text, @"SPEC-[A-Z][A-Z0-9-]*").Value is { Length: > 0 } v ? v : null;

    private static int CountLintErrors(string root)
    {
        var buf = new StringWriter();
        Lint.Run(root, buf);
        return buf.ToString().Split('\n').Count(l => l.StartsWith("✖", StringComparison.Ordinal));
    }

    private sealed class SpecInfo
    {
        public string Name { get; set; } = "";
        public string Statut { get; set; } = "";
        public List<string> DecisionLines { get; } = new();
        public List<(string Phase, string Contenu, string Jalon, string Statut)> PhaseRows { get; } = new();

        public static SpecInfo Load(string path)
        {
            var info = new SpecInfo { Name = Path.GetFileNameWithoutExtension(path) };
            string[] lines = File.ReadAllLines(path);
            bool inDecisions = false, inPhases = false, inFence = false;
            foreach (string line in lines)
            {
                string t = line.TrimStart();
                if (t.StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }

                if (inFence)
                {
                    continue;
                }

                if (line.StartsWith("**Statut**", StringComparison.Ordinal)
                    || line.StartsWith("**Status**", StringComparison.Ordinal))
                {
                    info.Statut = line.Substring(line.IndexOf(':') + 1).Trim();
                }

                if (line.StartsWith("## Décisions", StringComparison.Ordinal) || line.StartsWith("## Decisions", StringComparison.Ordinal)) { inDecisions = true; inPhases = false; continue; }
                if (line.StartsWith("## Phases", StringComparison.Ordinal)) { inPhases = true; inDecisions = false; continue; }
                if (line.StartsWith("## ", StringComparison.Ordinal)) { inDecisions = false; inPhases = false; }

                if (inDecisions && Regex.IsMatch(line, @"^-\s+\*\*D\d+\*\*"))
                {
                    info.DecisionLines.Add(line);
                }

                if (inPhases && Regex.IsMatch(line, @"^\|\s*P\d+\s*\|"))
                {
                    string[] cells = line.Split('|').Select(c => c.Trim()).ToArray();
                    if (cells.Length >= 5)
                    {
                        info.PhaseRows.Add((cells[1], cells[2], cells[3], cells[4]));
                    }
                }
            }

            return info;
        }

        public string PhaseContenu(string phaseId)
        {
            var row = PhaseRows.FirstOrDefault(r => r.Phase == phaseId);
            string contenu = row.Contenu?.Replace("`", "") ?? "";
            return contenu.Length > 28 ? contenu[..28].TrimEnd() + "…" : contenu;
        }

        public string NextJalon(string phaseId)
        {
            int idx = PhaseRows.FindIndex(r => r.Phase == phaseId);
            if (idx < 0)
            {
                return PhaseRows.Count > 0 ? $"{PhaseRows[0].Phase} — {PhaseRows[0].Jalon}" : "—";
            }

            var row = idx + 1 < PhaseRows.Count ? PhaseRows[idx + 1] : PhaseRows[idx];
            return $"{row.Phase} — {row.Jalon}";
        }
    }
}
