namespace Sdd;

/// <summary>
/// Genérateur des artefacts SDD (partage par `sdd init` et `sdd adopt`).
/// Les contenus proviennent de ressources embarquées bilingues
/// (resources/*.fr.md, resources/*.en.md) choisies selon la locale du projet.
/// Aucun commit, aucune ecrasure : le contrat humain garde la main.
/// </summary>
public static class Scaffold
{
    /// <summary>Marqueur « aucune spec en vol » de la locale demandée.</summary>
    public static string EnVolMarker(string? lang) => Locale.Normalize(lang) == "fr"
        ? "_(aucune — `sdd new` ajoute une entrée ici)_"
        : "_(none — `sdd new` adds an entry here)_";

    /// <summary>Titre de la section « spécifications en vol » de la locale demandée.</summary>
    public static string InFlightSection(string? lang) => Locale.Normalize(lang) == "fr"
        ? "## Spécifications en vol"
        : "## Specifications in flight";

    /// <summary>Ligne consignée dans AGENT_STATE pour un Brouillon.</summary>
    public static string InFlightEntry(string? lang, string famille, string date) => Locale.Normalize(lang) == "fr"
        ? $"- SPEC-{famille}.md — Brouillon v1.0 ({date}) — [À RATIFIER]"
        : $"- SPEC-{famille}.md — Draft v1.0 ({date}) — [TO RATIFY]";

    /// <summary>Fichiers a creer (chemin absolu -> contenu), sans toucher au disque.</summary>
    public static Dictionary<string, string> Build(string root, string nom, string? lang = null,
        string? backlogEntries = null, string? originNote = null)
    {
        string l = Locale.Normalize(lang);
        return new()
        {
            [Path.Combine(root, "sdd.toml")] = Toml(nom, l, originNote),
            [Path.Combine(root, "docs", "DOCTRINE.md")] = Program.Resource($"DOCTRINE.{l}.md"),
            [Path.Combine(root, "docs", "AGENT_STATE.md")] = AgentState(nom, l),
            [Path.Combine(root, "docs", "BACKLOG.md")] = Backlog(nom, backlogEntries, l),
            [Path.Combine(root, ".github", "workflows", "sdd-lint.yml")] = Workflow(),
        };
    }

    public static string Toml(string nom, string? lang, string? originNote) =>
        $"""
        # sdd.toml — métadonnées du projet (généré par `{(originNote ?? "sdd init")}`)
        [projet]
        nom = "{nom}"
        lang = "{Locale.Normalize(lang)}"   # fr | en — langue des artefacts générés

        [doctrine]
        version = "{Program.DoctrineVersion}"  # pin : docs/DOCTRINE.md, embarqué verbatim par la CLI

        """;

    public static string AgentState(string nom, string? lang = null) =>
        Program.Resource($"AGENT_STATE.{Locale.Normalize(lang)}.md")
            .Replace("{{PROJET}}", nom);

    public static string Backlog(string nom, string? entries, string? lang = null)
    {
        string l = Locale.Normalize(lang);
        string vide = l == "fr" ? "_(vide)_" : "_(empty)_";
        string tail = entries is { Length: > 0 } ? entries.TrimEnd() : vide;
        return Program.Resource($"BACKLOG.{l}.md")
            .Replace("{{PROJET}}", nom)
            .Replace("{{ENTRIES}}", tail);
    }

    /// <summary>CI gate fonctionnel (REQ-CLI09) — tests + lint, avec guard
    /// d'installation de la CLI (le dogfooding ne doit pas masquer un trou
    /// « command not found » pour les projets sans CLI vendue).</summary>
    public static string Workflow() =>
        """
        # CI gate SDD (REQ-CLI09) — bloquant sur erreur, tolérant aux warnings.
        name: sdd-lint
        on:
          push:
            branches: [ main ]
          pull_request:
        jobs:
          lint:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@v4
              - uses: actions/setup-dotnet@v4
                with:
                  dotnet-version: 10.0.x
              - name: tests SDD (régressions protégées par le gate)
                run: |
                  if [ -f tests/Sdd.Tests/Sdd.Tests.csproj ]; then
                    dotnet run --project tests/Sdd.Tests
                  else
                    echo "pas de tests/Sdd.Tests dans ce projet — skip"
                  fi
              - name: sdd lint --ci
                run: |
                  if [ -f src/Sdd.Cli/Sdd.Cli.csproj ]; then
                    dotnet run --project src/Sdd.Cli -- lint --ci
                  elif command -v sdd >/dev/null 2>&1; then
                    sdd lint --ci   # CLI déjà installée (dotnet tool install -g sdd)
                  else
                    echo '::error title=CI SDD::CLI « sdd » introuvable dans ce runner : vendez src/Sdd.Cli dans le repo ou installez l outil (dotnet tool install -g sdd --add-source <feed>).' >&2
                    exit 2
                  fi

        """;
}
