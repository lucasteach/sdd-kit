namespace Sdd;

/// <summary>
/// Locale de projet, déclarée dans <c>sdd.toml</c> : <c>[projet] lang = "fr" | "en"</c>.
/// Défaut <c>en</c> : le marché public est international et les artefacts sont
/// générés dans la langue du projet.
/// Les messages de la CLI et le dashboard restent en français jusqu'à v1.2
/// (hors périmètre de la bornée v1.1).
/// </summary>
public static class Locale
{
    public const string Default = "en";
    public static readonly string[] Supported = { "fr", "en" };

    public static bool IsSupported(string? lang) =>
        lang is not null && Supported.Contains(lang.ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>Normalise une locale : toute valeur absente ou inconnue retombe sur le défaut.</summary>
    public static string Normalize(string? lang) =>
        IsSupported(lang) ? lang!.ToLowerInvariant() : Default;

    /// <summary>Liste lisible pour les messages d'usage (ex. « fr|en »).</summary>
    public static string Describe() => string.Join("|", Supported);
}
