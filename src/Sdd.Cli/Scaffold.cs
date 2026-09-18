namespace Sdd;

/// <summary>
/// Genérateur des artefacts SDD (partage par `sdd init` et `sdd adopt`).
/// Aucun commit, aucune ecrasure : le contrat humain garde la main.
/// </summary>
public static class Scaffold
{
    public const string EnVolMarker = "_(aucune — `sdd new` ajoute une entrée ici)_";

    /// <summary>Fichiers a creer (chemin absolu -> contenu), sans toucher au disque.</summary>
    public static Dictionary<string, string> Build(string root, string nom, string? backlogEntries = null, string? originNote = null) => new()
    {
        [Path.Combine(root, "sdd.toml")] = Toml(nom, originNote),
        [Path.Combine(root, "docs", "DOCTRINE.md")] = Program.Resource("DOCTRINE.md"),
        [Path.Combine(root, "docs", "AGENT_STATE.md")] = AgentState(nom),
        [Path.Combine(root, "docs", "BACKLOG.md")] = Backlog(nom, backlogEntries),
        [Path.Combine(root, ".github", "workflows", "sdd-lint.yml")] = Workflow(),
    };

    public static string Toml(string nom, string? originNote) =>
        $"""
        # sdd.toml — métadonnées du projet (généré par `{(originNote ?? "sdd init")}`)
        [projet]
        nom = "{nom}"

        [doctrine]
        version = "{Program.DoctrineVersion}"  # pin : docs/DOCTRINE.md, embarqué verbatim par la CLI

        """;

    public static string AgentState(string nom) =>
        $"""
        # AGENT_STATE — {nom}

        <!-- Mémoire de continuité inter-sessions. Source de vérité du repo. -->

        ## Convention de reprise

        Le **premier message** d'un nouveau chat sur ce projet doit être :

        > « Lee docs/AGENT_STATE.md y continúa »

        ## État courant

        | Champ | Valeur |
        |---|---|
        | Phase active | — |
        | Spec de référence | — |
        | Next action | — |

        ## Spécifications en vol

        {EnVolMarker}

        ## Table des commits de session

        <!-- Une ligne par session : hash, date, agent, résumé, prochaine action. -->

        | Hash | Date | Agent | Résumé | Prochaine action |
        |---|---|---|---|---|

        """;

    public static string Backlog(string nom, string? entries)
    {
        string text =
            $"""
            # BACKLOG — {nom}

            Registre des idées, dettes et demandes reportées du projet.

            ## Règle anti-oubli

            > **Toute idée reportée s'enregistre ici dans le commit qui la pose.**

            Quand une idée, amélioration ou correction est repoussée hors du périmètre
            de la tâche en cours, elle est ajoutée à ce fichier **dans le même commit**
            que celui qui la postpone. Aucune idée reportée ne vit uniquement dans une
            conversation.

            ## Entrées

            <!-- Format : - [ ] (YYYY-MM-DD, origine) description → phase/version visée -->

            """;
        string tail = entries is { Length: > 0 } ? entries.TrimEnd() + "\n" : "_(vide)_\n";
        return text + tail;
    }

    /// <summary>CI gate fonctionnel (REQ-CLI09) — remplace le placeholder P1/P2.</summary>
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
