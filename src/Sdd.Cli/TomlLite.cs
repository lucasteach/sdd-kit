using System.Text;
using System.Text.RegularExpressions;

namespace Sdd;

public sealed record Waiver(string Regle, string Portee, string Justification, int Line = 0);

/// <summary>
/// Lecteur sdd.toml minimal (sections [projet], [doctrine], [[waiver]]).
/// Écrire un parser maison evite toute dependance NuGet (regle dure P1/P2).
/// </summary>
public static class TomlLite
{
    public sealed class ProjectMeta
    {
        public string Nom { get; set; } = "";
        public string Doctrine { get; set; } = "";
        public List<Waiver> Waivers { get; } = new();
        public bool Found { get; set; }
    }

    public static ProjectMeta Load(string root)
    {
        var meta = new ProjectMeta();
        string path = Path.Combine(root, "sdd.toml");
        if (!File.Exists(path))
        {
            return meta;
        }

        meta.Found = true;
        string section = "";
        Waiver? current = null;
        var kv = new Regex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*""([^""]*)""\s*(?:#.*)?$");
        var mlOpener = new Regex("^([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(\"{3}|'{3})(.*)$");
        string? mlKey = null, mlDelim = null;
        var mlBuf = new StringBuilder();

        void ApplyPair(string key, string value)
        {
            switch (section)
            {
                case "projet":
                    if (key == "nom") meta.Nom = value;
                    break;
                case "doctrine":
                    if (key == "version") meta.Doctrine = value;
                    break;
                case "waiver" when current is not null:
                    meta.Waivers[^1] = meta.Waivers[^1] with
                    {
                        Regle = key == "regle" ? value : meta.Waivers[^1].Regle,
                        Portee = key == "portee" ? value : meta.Waivers[^1].Portee,
                        Justification = key == "justification" ? value : meta.Waivers[^1].Justification,
                    };
                    break;
            }
        }

        int lineNo = 0;
        foreach (string raw in File.ReadAllLines(path))
        {
            lineNo++;
            string line = raw.Trim();

            // suite d'une chaine multiligne (""" … """ ou ''' … ''')
            if (mlKey is not null)
            {
                if (line.EndsWith(mlDelim!, StringComparison.Ordinal))
                {
                    string endToken = line[..Math.Max(0, line.Length - mlDelim!.Length)];
                    if (mlBuf.Length > 0)
                    {
                        mlBuf.Append('\n');
                    }

                    mlBuf.Append(endToken.Trim());
                    ApplyPair(mlKey, mlBuf.ToString().Trim());
                    mlKey = mlDelim = null;
                    mlBuf.Clear();
                }
                else
                {
                    if (mlBuf.Length > 0)
                    {
                        mlBuf.Append('\n');
                    }

                    mlBuf.Append(line);
                }

                continue;
            }

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line == "[projet]") { section = "projet"; continue; }
            if (line == "[doctrine]") { section = "doctrine"; continue; }
            if (line == "[[waiver]]")
            {
                section = "waiver";
                current = new Waiver("", "", "", lineNo);
                meta.Waivers.Add(current);
                continue;
            }

            // ouverture d'une chaine multiligne ? key = """ / key = '''
            Match mo = mlOpener.Match(line);
            if (mo.Success)
            {
                string key = mo.Groups[1].Value;
                string delim = mo.Groups[2].Value;
                string inline = mo.Groups[3].Value;
                int close = inline.LastIndexOf(delim, StringComparison.Ordinal);
                if (close >= 0)
                {
                    // """value""" sur une seule ligne physique
                    ApplyPair(key, inline[..close].Trim());
                    continue;
                }

                mlKey = key;
                mlDelim = delim;
                mlBuf.Clear();
                if (inline.Length > 0)
                {
                    mlBuf.Append(inline);
                }

                continue;
            }

            Match m = kv.Match(line);
            if (!m.Success)
            {
                continue;
            }

            ApplyPair(m.Groups[1].Value, m.Groups[2].Value);
        }

        return meta;
    }
}
