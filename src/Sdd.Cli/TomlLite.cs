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

        int lineNo = 0;
        foreach (string raw in File.ReadAllLines(path))
        {
            lineNo++;
            string line = raw.Trim();
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

            Match m = kv.Match(line);
            if (!m.Success)
            {
                continue;
            }

            string key = m.Groups[1].Value;
            string value = m.Groups[2].Value;
            switch (section)
            {
                case "projet":
                    if (key == "nom") meta.Nom = value;
                    break;
                case "doctrine":
                    if (key == "version") meta.Doctrine = value;
                    break;
                case "waiver" when current is not null:
                    current = current with
                    {
                        Regle = key == "regle" ? value : current.Regle,
                        Portee = key == "portee" ? value : current.Portee,
                        Justification = key == "justification" ? value : current.Justification,
                    };
                    meta.Waivers[^1] = current;
                    break;
            }
        }

        return meta;
    }
}
