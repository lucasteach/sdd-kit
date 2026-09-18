using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// sdd adopt — REQ-CLI02, onramp brownfield : audit guide du projet existant,
/// chaque constat devient une entree BACKLOG (origine fichier:ligne, statut
/// ouvert), puis creation des infrastructures SDD sans toucher au code ni a
/// l'historique git du projet (aucun commit).
/// Faux negatifs assumés (documentes) : patterns limites aux formats connus ;
/// les liens ne sont verifies que si le reseau est disponible ; les « ecrans »
/// sont approximes par fichier.
/// </summary>
public static class Adopt
{
    private static readonly string[] SkipDirs =
        { ".git", "bin", "obj", "node_modules", "dist", "build", ".vs", ".idea", "testresults", "wwwroot/lib" };
    private static readonly HashSet<string> ScanExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".razor", ".cshtml", ".html", ".js", ".ts", ".cs", ".json", ".md", ".yml", ".yaml",
    };

    private sealed record Constat(string Kind, string File, int Line, string Description, string Priority);

    public static int Run(string root, string[] args, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        if (args.Length != 2 || args[0] != "--projet" || string.IsNullOrWhiteSpace(args[1]))
        {
            writer.WriteLine("usage : sdd adopt --projet <nom>   (dans le dossier du projet existant)");
            return 1;
        }

        string nom = args[1];
        if (!Regex.IsMatch(nom, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$"))
        {
            writer.WriteLine($"✖ nom de projet invalide : « {nom} »");
            return 1;
        }

        if (File.Exists(Path.Combine(root, "sdd.toml")))
        {
            writer.WriteLine("✖ projet déjà initialisé (sdd.toml présent) — `sdd adopt` n'écrase pas une structure SDD existante.");
            return 1;
        }

        var files = ScanFiles(root);
        var constats = new List<Constat>();
        int linksChecked = 0, linksOffline = 0;

        DetectOrphanRoutes(files, constats);
        DetectMocks(files, constats);
        (int checkedN, int offlineN) = DetectDeadLinks(files, constats);
        linksChecked = checkedN;
        linksOffline = offlineN;
        DetectFalseConfirmations(files, constats);
        DetectContradictoryMetrics(files, constats);

        string date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var backlogEntries = new StringBuilder();
        for (int i = 0; i < constats.Count; i++)
        {
            Constat h = constats[i];
            backlogEntries.AppendLine(
                $"- [ ] BUK-{i + 1:D3} ({date}, origine : {h.File}:{h.Line} — sdd adopt) {h.Kind} — {h.Description} (priorité {h.Priority}) → à trier en spec");
        }

        writer.WriteLine($"Audit brownfield « {nom} » — {files.Count} fichiers scannés.");
        if (constats.Count == 0)
        {
            writer.WriteLine("  aucun constat détecté (patterns connus ; faux négatifs possibles sur code exotique).");
        }
        else
        {
            foreach (Constat h in constats)
            {
                writer.WriteLine($"  ✖ {h.File}:{h.Line} — {h.Kind} : {h.Description} [{h.Priority}]");
            }
        }

        writer.WriteLine($"  liens HTTP vérifiés : {linksChecked}" + (linksOffline > 0 ? $" — non vérifiés (hors-ligne/DNS) : {linksOffline}" : ""));

        var artifacts = Scaffold.Build(root, nom,
            constats.Count > 0 ? backlogEntries.ToString() : null, originNote: "sdd adopt");
        string backlogPath = Path.Combine(root, "docs", "BACKLOG.md");
        bool entriesWritten = constats.Count == 0; // rien à écrire → rien à promettre
        foreach ((string path, string content) in artifacts)
        {
            try
            {
                if (File.Exists(path))
                {
                    if (path == backlogPath && constats.Count > 0)
                    {
                        // BACKLOG préexistant (cas brownfield typique) : APPEND sous
                        // marqueur daté, jamais d'écrasement ni d'abandon silencieux.
                        AppendBacklog(path, nom, date, backlogEntries.ToString());
                        entriesWritten = true;
                        writer.WriteLine($"  mis à jour (append sous marqueur)  {Program.Relative(root, path)}");
                        continue;
                    }

                    writer.WriteLine($"  existant, non écrasé : {Program.Relative(root, path)}");
                    continue;
                }

                Program.Write(path, content);
                if (path == backlogPath && constats.Count > 0)
                {
                    entriesWritten = true;
                }

                writer.WriteLine($"  créé  {Program.Relative(root, path)}");
            }
            catch (Exception ex)
            {
                writer.WriteLine($"✖ écriture impossible sur {Program.Relative(root, path)} : {ex.Message}");
                writer.WriteLine("  les constats ne sont PAS enregistrés — corrigez et relancez `sdd adopt`.");
                return 1;
            }
        }

        if (!Directory.Exists(Path.Combine(root, "docs", "specs")))
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "docs", "specs"));
            }
            catch (Exception ex)
            {
                writer.WriteLine($"✖ impossible de créer docs/specs/ : {ex.Message}");
                return 1;
            }
        }

        writer.WriteLine();
        if (constats.Count > 0 && entriesWritten)
        {
            writer.WriteLine($"→ {constats.Count} constats enregistrés dans docs/BACKLOG.md (BUK-001…BUK-{constats.Count:D3}), statut ouvert.");
        }
        else if (constats.Count == 0)
        {
            writer.WriteLine("→ aucune entrée BACKLOG créée (audit sans constat sur les patterns connus).");
        }

        writer.WriteLine("  L'historique et le code du projet sont intacts ; la CLI ne committe pas (contrat humain).");
        writer.WriteLine("  Prochaine étape : commiter l'infrastructure, puis `sdd new <FAMILLE>` pour spec-couvrir les BUK prioritaires.");
        return 0;
    }

    // ---------- collecte ----------

    /// <summary>Append des BUK d'audit dans un BACKLOG préexistant, sous un marqueur
    /// daté `&lt;!-- sdd-adopt nom date --&gt;`. Le contenu existant n'est jamais modifié.</summary>
    private static void AppendBacklog(string path, string nom, string date, string entries)
    {
        var lines = File.ReadAllLines(path).ToList();
        string marker = $"<!-- sdd-adopt {nom} {date} -->";
        var block = new List<string> { "", marker, "" };
        block.AddRange(entries.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));

        int header = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("## Entrées", StringComparison.Ordinal))
            {
                header = i;
                break;
            }
        }

        if (header < 0)
        {
            lines.Add("");
            lines.Add("## Entrées");
            lines.AddRange(block);
        }
        else
        {
            int end = header + 1;
            while (end < lines.Count && !lines[end].StartsWith("## ", StringComparison.Ordinal))
            {
                end++;
            }

            lines.InsertRange(end, block);
        }

        File.WriteAllText(path, string.Join('\n', lines) + "\n", new System.Text.UTF8Encoding(false));
    }

    private sealed record SrcFile(string Rel, string[] Lines);

    private static List<SrcFile> ScanFiles(string root)
    {
        var result = new List<SrcFile>();
        var queue = new Queue<string>();
        queue.Enqueue(root);
        while (queue.Count > 0 && result.Count < 2000)
        {
            string dir = queue.Dequeue();
            string dirName = Path.GetFileName(dir);
            if (result.Count > 0 && SkipDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string sub in Directory.GetDirectories(dir))
            {
                queue.Enqueue(sub);
            }

            foreach (string file in Directory.GetFiles(dir))
            {
                if (!ScanExt.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                var info = new FileInfo(file);
                if (info.Length > 512 * 1024)
                {
                    continue;
                }

                string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (rel.StartsWith("docs/", StringComparison.Ordinal) || rel == "sdd.toml" || rel.StartsWith(".github/", StringComparison.Ordinal))
                {
                    continue; // l'infrastructure SDD elle-même n'est pas un constat brownfield
                }

                result.Add(new SrcFile(rel, File.ReadAllLines(file)));
            }
        }

        return result;
    }

    // ---------- patterns (Annexe P4) ----------

    private static void DetectOrphanRoutes(List<SrcFile> files, List<Constat> found)
    {
        var pageRoutes = new List<(SrcFile File, int Line, string Route)>();
        foreach (SrcFile f in files)
        {
            if (!f.Rel.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(f.Rel);
            if (name.StartsWith('_') || name is "App" or "Host")
            {
                continue;
            }

            bool hasPage = false;
            for (int i = 0; i < f.Lines.Length; i++)
            {
                Match m = Regex.Match(f.Lines[i], @"@page\s+""([^""]+)""");
                if (m.Success)
                {
                    hasPage = true;
                    pageRoutes.Add((f, i + 1, m.Groups[1].Value));
                }
            }

            bool underPages = f.Rel.Contains("Pages/", StringComparison.OrdinalIgnoreCase)
                              || f.Rel.Contains("pages/", StringComparison.OrdinalIgnoreCase);
            if (!hasPage && underPages)
            {
                found.Add(new Constat("page sans @page", f.Rel, 1,
                    "fichier sous Pages/ sans directive @page (route inatteignable ou composant mal placé)", "P3"));
            }
        }

        foreach ((SrcFile f, int line, string route) in pageRoutes)
        {
            bool referenced = files.Any(other => other.Rel != f.Rel
                && other.Lines.Any(l => l.Contains($"\"{route}\"", StringComparison.Ordinal)
                                     || l.Contains(route, StringComparison.Ordinal)));
            if (!referenced)
            {
                found.Add(new Constat("route orpheline", f.Rel, line,
                    $"« {route} » n'est référencée dans aucune navigation ni lien", "P2"));
            }
        }
    }

    private static void DetectMocks(List<SrcFile> files, List<Constat> found)
    {
        var rules = new (Regex Rx, string Kind, string Desc, string Prio)[]
        {
            (new Regex(@"\b(TODO|FIXME)\b"), "mock/placeholder", "TODO/FIXME en dur dans le code", "P2"),
            (new Regex(@"à implémenter|placeholder|lorem ipsum", RegexOptions.IgnoreCase), "mock/placeholder", "texte placeholder non implémenté", "P3"),
            (new Regex(@"(data|datasets)\s*:\s*\[\s*\]"), "mock/placeholder", "graphique initialisé sans données réelles", "P2"),
        };

        foreach (SrcFile f in files)
        {
            foreach ((Regex rx, string kind, string desc, string prio) in rules)
            {
                for (int i = 0; i < f.Lines.Length; i++)
                {
                    if (rx.IsMatch(f.Lines[i]))
                    {
                        found.Add(new Constat(kind, f.Rel, i + 1, desc, prio));
                        break; // une occurrence par fichier et par pattern
                    }
                }
            }
        }
    }

    private static (int Checked, int Offline) DetectDeadLinks(List<SrcFile> files, List<Constat> found)
    {
        var urls = new List<(SrcFile F, int Line, string Url)>();
        var rx = new Regex("(?:href|src)=\"(https?://[^\"]+)\"");
        foreach (SrcFile f in files)
        {
            for (int i = 0; i < f.Lines.Length && urls.Count < 25; i++)
            {
                foreach (Match m in rx.Matches(f.Lines[i]))
                {
                    string url = m.Groups[1].Value;
                    if (url.Contains("localhost", StringComparison.OrdinalIgnoreCase) || url.Contains("127.0.0.1"))
                    {
                        continue;
                    }

                    urls.Add((f, i + 1, url));
                }
            }
        }

        if (Environment.GetEnvironmentVariable("SDD_SKIP_NETWORK") == "1")
        {
            return (0, urls.Count);
        }

        int checkedN = 0, offline = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(4),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("sdd-adopt/0.1 (audit brownfield)");

        foreach ((SrcFile f, int line, string url) in urls)
        {
            if (!seen.Add(url))
            {
                continue;
            }

            checkedN++;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage resp = http.Send(req, HttpCompletionOption.ResponseHeadersRead);
                if ((int)resp.StatusCode >= 400)
                {
                    found.Add(new Constat("lien mort", f.Rel, line, $"{url} répond HTTP {(int)resp.StatusCode}", "P1"));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                offline++;
            }
        }

        return (checkedN, offline);
    }

    private static void DetectFalseConfirmations(List<SrcFile> files, List<Constat> found)
    {
        var success = new Regex("succès|succes|success|terminé|termine|complété|completee|complète", RegexOptions.IgnoreCase);
        var zero = new Regex(@"Count\s*==\s*0|Length\s*==\s*0|\bN\s*=\s*0|\(\s*0\s*\)|:\s*0\b|—\s*0\b|\b0\s+(éléments|items|enregistrements|résultats|results|records|tâches)",
            RegexOptions.IgnoreCase);

        foreach (SrcFile f in files)
        {
            for (int i = 0; i < f.Lines.Length; i++)
            {
                if (!success.IsMatch(f.Lines[i]))
                {
                    continue;
                }

                int window = Math.Min(f.Lines.Length, i + 3);
                for (int j = Math.Max(0, i - 2); j < window; j++)
                {
                    if (zero.IsMatch(f.Lines[j]))
                    {
                        found.Add(new Constat("fausse confirmation", f.Rel, i + 1,
                            "message de succès affiché alors que le compteur est à 0 (règle 9 : succès seulement si N > 0)", "P1"));
                        break;
                    }
                }

                break; // une occurrence par fichier
            }
        }
    }

    private static void DetectContradictoryMetrics(List<SrcFile> files, List<Constat> found)
    {
        var hundred = new Regex(@"100\s*%");
        var nothing = new Regex(@"\b0\s+(items|éléments|tâches|dossiers|traités|résultats|results|records|réalisés|done)", RegexOptions.IgnoreCase);
        foreach (SrcFile f in files)
        {
            int hLine = -1, zLine = -1;
            for (int i = 0; i < f.Lines.Length; i++)
            {
                if (hLine < 0 && hundred.IsMatch(f.Lines[i])) hLine = i + 1;
                if (zLine < 0 && nothing.IsMatch(f.Lines[i])) zLine = i + 1;
            }

            if (hLine > 0 && zLine > 0)
            {
                found.Add(new Constat("métriques contradictoires", f.Rel, Math.Min(hLine, zLine),
                    $"« 100 % » (ligne {hLine}) cohabite avec un compteur à 0 (ligne {zLine}) sur le même écran", "P1"));
            }
        }
    }
}
