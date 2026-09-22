using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace Sdd;

/// <summary>Petit wrapper git (BCL seul, zero dependance).</summary>
public static class Git
{
    public static (int exit, string stdout, string stderr) Run(string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = Process.Start(psi)!;
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, stdout.Replace("\r\n", "\n"), stderr.Replace("\r\n", "\n"));
    }

    public static bool IsRepo(string dir) => Run(dir, "rev-parse", "--show-toplevel").exit == 0;
}

/// <summary>Vue partagee d'un fichier SPEC (lectures/edits conventions, pas de prose inventee).</summary>
public sealed class SpecModel
{
    public string Path { get; }
    public string Name { get; }
    public List<string> Lines { get; }

    private SpecModel(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        Lines = File.ReadAllLines(path).ToList();
    }

    public static SpecModel Load(string path) => new(path);

    public static string? ResolvePath(string root, string specArg)
    {
        string name = specArg.EndsWith(".md", StringComparison.Ordinal) ? specArg : specArg + ".md";
        if (!name.StartsWith("SPEC-", StringComparison.OrdinalIgnoreCase))
        {
            name = "SPEC-" + specArg;
            if (!name.EndsWith(".md", StringComparison.Ordinal))
            {
                name += ".md";
            }
        }

        string candidate = System.IO.Path.Combine(root, "docs", "specs", name);
        return File.Exists(candidate) ? candidate : null;
    }

    public int FindLine(string prefix, int from = 0)
    {
        for (int i = from; i < Lines.Count; i++)
        {
            if (Lines[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public int SectionStart(string header) => FindLine(header) + 1;

    /// <summary>Première section trouvée parmi plusieurs titres acceptés (FR/EN).</summary>
    public int SectionStartAny(params string[] headers)
    {
        foreach (string h in headers)
        {
            int i = FindLine(h);
            if (i >= 0)
            {
                return i + 1;
            }
        }

        return -1;
    }

    /// <summary>Ligne d'un titre accepté (FR/EN), ou -1.</summary>
    public int FindLineAny(params string[] headers)
    {
        foreach (string h in headers)
        {
            int i = FindLine(h);
            if (i >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    public int SectionEnd(int start)
    {
        for (int i = start; i < Lines.Count; i++)
        {
            if (Lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return Lines.Count;
    }

    /// <summary>Lignes de decision « - **Dn** … » avec index.</summary>
    public List<(int Index, string Text)> DecisionLines()
    {
        var result = new List<(int, string)>();
        int start = SectionStartAny("## Décisions", "## Decisions");
        if (start <= 0)
        {
            return result;
        }

        int end = SectionEnd(start);
        for (int i = start; i < end; i++)
        {
            if (Lines[i].StartsWith("- **D", StringComparison.Ordinal))
            {
                result.Add((i, Lines[i]));
            }
        }

        return result;
    }

    /// <summary>Blocs REQ : (ligne de titre, lignes du bloc, identifiant REQ).</summary>
    public List<(string Id, int Start, int End)> ReqBlocks()
    {
        var heads = new List<int>();
        for (int i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].StartsWith("### REQ-", StringComparison.Ordinal))
            {
                heads.Add(i);
            }
        }

        var blocks = new List<(string, int, int)>();
        for (int k = 0; k < heads.Count; k++)
        {
            int start = heads[k];
            int end = k + 1 < heads.Count ? heads[k + 1] : Lines.Count;
            while (end > start + 1 && Lines[end - 1].Trim().Length == 0)
            {
                end--;
            }

            string id = System.Text.RegularExpressions.Regex
                .Match(Lines[start], @"^###\s+(REQ-[A-Za-z0-9-]+)").Groups[1].Value;
            blocks.Add((id, start, end));
        }

        return blocks;
    }

    /// <summary>Lignes du tableau des phases : (Phase, Contenu, Jalon, Statut, ligne).</summary>
    public List<(string Phase, string Contenu, string Jalon, string Statut, int Line)> PhaseRows()
    {
        var result = new List<(string, string, string, string, int)>();
        int start = SectionStart("## Phases");
        if (start <= 0)
        {
            return result;
        }

        int end = SectionEnd(start);
        for (int i = start; i < end; i++)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(Lines[i], @"^\|\s*P\d+\s*\|"))
            {
                string[] cells = Lines[i].Split('|').Select(c => c.Trim()).ToArray();
                if (cells.Length >= 5)
                {
                    result.Add((cells[1], cells[2], cells[3], cells[4], i));
                }
            }
        }

        return result;
    }

    public string Version()
    {
        int i = FindLine("**Version**");
        if (i < 0)
        {
            return "";
        }

        var m = System.Text.RegularExpressions.Regex.Match(Lines[i], @"(\d+)\.(\d+)");
        return m.Success ? $"{m.Groups[1].Value}.{m.Groups[2].Value}" : "";
    }

    public void Save() => File.WriteAllText(Path, string.Join('\n', Lines) + "\n", new UTF8Encoding(false));

    /// <summary>
    /// Heuristique REQ→phase partagée (ex-Trace.cs/116 + Brief.cs/159, DRY) :
    /// la REQ est liée à une phase si le Contenu cite l'id littérale, le mot
    /// entre parenthèses du titre, ou un mot-signature (≥ 5 lettres, hors
    /// stopwords) du titre. Non normative : `sdd trace`/`agent-brief` l'assument
    /// comme approximation documentee.
    /// </summary>
    public bool PhaseMentions(string contenu, string req)
    {
        string c = contenu.ToLowerInvariant().Replace("`", "");
        if (c.Contains(req.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return true;
        }

        var range = ReqBlocks().FirstOrDefault(b => b.Id == req);
        if (range.Start == 0 && range.Id is null)
        {
            return false;
        }

        string title = Lines[range.Start];
        int colon = title.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        string tail = title[(colon + 1)..];
        Match paren = Regex.Match(tail, @"\(([^)]+)\)");
        if (paren.Success && c.Contains(paren.Groups[1].Value.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return true;
        }

        return Regex.Matches(tail.ToLowerInvariant(), @"[a-zà-ÿ]{5,}")
            .Where(w => w.Value is not ("règles" or "spec" or "projet"))
            .Any(w => c.Contains(w.Value, StringComparison.Ordinal));
    }
}
