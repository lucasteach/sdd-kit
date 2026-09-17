using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

/// <summary>
/// sdd agent-brief — REQ-CLI08 : prompt texte auto-suffisant pour une
/// agent borne (perimetre, decisions incrustees, regles dures, formats
/// commit/report). La CLI assemble la spec ; elle n'invente aucune prose.
/// </summary>
public static class Brief
{
    public static int Run(string root, string[] args, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        string agent = "agent";
        var positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--pour" && i + 1 < args.Length)
            {
                agent = args[++i];
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        if (positional.Count != 2)
        {
            writer.WriteLine("usage : sdd agent-brief <SPEC> <Pn> --pour <agent>");
            return 1;
        }

        string? specPath = SpecModel.ResolvePath(root, positional[0]);
        string phase = positional[1].ToUpperInvariant();
        if (specPath is null)
        {
            writer.WriteLine($"✖ spec introuvable : docs/specs/{positional[0]}");
            return 1;
        }

        var spec = SpecModel.Load(specPath);
        var rows = spec.PhaseRows();
        var current = rows.FirstOrDefault(r => r.Phase.Equals(phase, StringComparison.OrdinalIgnoreCase));
        if (current.Phase is null or { Length: 0 })
        {
            writer.WriteLine($"✖ phase « {phase} » absente du tableau « ## Phases » de {spec.Name}.md");
            return 1;
        }

        string doctrinePath = Path.Combine(root, "docs", "DOCTRINE.md");
        string doctrine = File.Exists(doctrinePath)
            ? File.ReadAllText(doctrinePath).Replace("\r\n", "\n").TrimEnd()
            : Resource("DOCTRINE.md").TrimEnd();
        string doctrineVersion = Regex.Match(doctrine, @"v(\d+\.\d+)").Groups[1].Value;
        var meta = TomlLite.Load(root);

        var reqs = spec.ReqBlocks()
            .Where(b => PhaseMentions(current.Contenu, b.Id, spec))
            .ToList();

        var o = new StringBuilder();
        string date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        o.AppendLine($"# Tâche bornée : {spec.Name} — {phase} ({Strip(current.Contenu)})");
        o.AppendLine();
        o.AppendLine($"Généré par `sdd agent-brief` le {date} — destinataire : {agent}.");
        o.AppendLine("Contexte obligatoire à lire avant d'exécuter : la spec complète ci-dessous (sections");
        o.AppendLine("copiées verbatim), docs/AGENT_STATE.md, docs/BACKLOG.md, sdd.toml.");
        o.AppendLine();
        o.AppendLine("## Périmètre strict");
        o.AppendLine();
        o.AppendLine($"Inclus (uniquement {phase}) :");
        o.AppendLine($"- Contenu : {current.Contenu}");
        o.AppendLine($"- Jalon visible : {current.Jalon}");
        o.AppendLine();
        o.AppendLine("Exclus (ne pas toucher, même « par erreur ») :");
        foreach (var r in rows.Where(r => r.Phase != current.Phase))
        {
            o.AppendLine($"- {r.Phase} : {r.Contenu}");
        }

        o.AppendLine();
        if (reqs.Count > 0)
        {
            o.AppendLine($"## Exigences couvertes ({reqs.Count} REQ, verbatim de la spec)");
            o.AppendLine();
            foreach ((string id, int start, int end) in reqs)
            {
                foreach (string line in spec.Lines[start..end])
                {
                    o.AppendLine(line);
                }

                o.AppendLine();
            }
        }
        else
        {
            o.AppendLine("## Exigences couvertes");
            o.AppendLine();
            o.AppendLine("— (aucune REQ du tableau ne cite explicitement cette phase : lire la spec");
            o.AppendLine("entière et borner le travail au Contenu/Jalon de la phase ci-dessus)");
            o.AppendLine();
        }

        var decisions = spec.DecisionLines()
            .Where(d => d.Text.Contains("RATIFIÉE", StringComparison.OrdinalIgnoreCase)
                        || d.Text.Contains("approuvée", StringComparison.OrdinalIgnoreCase))
            .ToList();
        o.AppendLine("## Décisions du responsable déjà ratifiées (contraintes, pas des suggestions)");
        o.AppendLine();
        if (decisions.Count > 0)
        {
            foreach ((_, string text) in decisions)
            {
                o.AppendLine(text);
            }
        }
        else
        {
            o.AppendLine("— (aucune décision ratifiée dans cette spec)");
        }

        o.AppendLine();
        o.AppendLine($"## Règles dures applicables (DOCTRINE v{(doctrineVersion.Length > 0 ? doctrineVersion : meta.Doctrine)}, verbatim)");
        o.AppendLine();
        o.AppendLine(doctrine);
        o.AppendLine();
        o.AppendLine("## Format de commit attendu");
        o.AppendLine();
        o.AppendLine("- `type(scope): sujet` ; une tâche bornée = un commit (ou un commit par livrable");
        o.AppendLine("  atomique) ; tree propre après chaque commit.");
        o.AppendLine("- Mentionner les REQ dans le sujet ou le corps (`Implements REQ-…`, `Closes REQ-…`)");
        o.AppendLine("  pour que `sdd trace` fonctionne (convention de commit-msg, Annexe P3).");
        o.AppendLine("- Idée repoussée → entrée BACKLOG dans le même commit (règle anti-oubli).");
        o.AppendLine();
        o.AppendLine("## Format de report attendu");
        o.AppendLine();
        o.AppendLine("- Hash + tree + sorties réelles des démos de validation + justifications");
        o.AppendLine("  (toute dépendance non triviale, tout warning toléré, toute décision d'implémentation).");
        o.AppendLine("- Zéro fausse confirmation : ce qui n'est pas vérifié est déclaré non vérifié.");
        o.AppendLine();
        o.AppendLine("## Validation");
        o.AppendLine();
        o.AppendLine($"- {current.Jalon} — vérifiable par le responsable ; tests unitaires verts exigés.");
        o.AppendLine($"- Après la tâche : `sdd lint` doit retourner 0 erreur sur le repo.");

        writer.Write(o.ToString());
        return 0;
    }

    private static string Strip(string s) => s.Replace("`", "");

    /// <summary>Même heuristique de mapping REQ→phase que `sdd trace`.</summary>
    private static bool PhaseMentions(string contenu, string req, SpecModel spec)
    {
        string c = contenu.ToLowerInvariant().Replace("`", "");
        if (c.Contains(req.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return true;
        }

        var range = spec.ReqBlocks().First(b => b.Id == req);
        string title = spec.Lines[range.Start];
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

        return Regex.Matches(tail.ToLowerInvariant(), @"[a-zèéûîôàû]{5,}")
            .Where(w => w.Value is not ("règles" or "spec" or "projet"))
            .Any(w => c.Contains(w.Value, StringComparison.Ordinal));
    }

    private static string Resource(string suffix)
    {
        Assembly asm = typeof(Brief).Assembly;
        string name = asm.GetManifestResourceNames().First(n => n.EndsWith(suffix, StringComparison.Ordinal));
        using Stream s = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(s);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
