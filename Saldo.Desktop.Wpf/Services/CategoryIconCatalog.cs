using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MahApps.Metro.IconPacks;

namespace Saldo.Desktop.Wpf.Services;

public sealed record CategoryIconItem(string Key, string Name, PackIconMaterialKind Kind, string SearchText)
{
    public string DisplayName { get; } = Regex.Replace(Name, "([a-z0-9])([A-Z])", "$1 $2");
}

/// <summary>GUI-only MDI catalog. Metadata is cached; controls are created only for the current page.</summary>
public static class CategoryIconCatalog
{
    private static readonly Dictionary<string, string> PolishAliases = new()
    {
        ["Home"] = "dom mieszkanie czynsz", ["Cart"] = "koszyk zakupy supermarket",
        ["SilverwareForkKnife"] = "jedzenie restauracja posilek", ["Car"] = "samochod transport paliwo",
        ["Paw"] = "pies kot zwierzeta lapa", ["HeartPulse"] = "zdrowie lekarz",
        ["Gift"] = "prezenty darowizny", ["TshirtCrew"] = "ubior ubrania",
        ["Wallet"] = "portfel pieniadze wynagrodzenie", ["Cash"] = "gotowka pieniadze",
        ["Bank"] = "bank kredyt odsetki", ["Phone"] = "telefon media",
        ["Wifi"] = "internet media", ["Cigarette"] = "papierosy uzywki",
        ["GlassWine"] = "wino alkohol uzywki", ["Briefcase"] = "praca wynagrodzenie",
        ["Dog"] = "pies zwierzeta", ["Cat"] = "kot zwierzeta", ["Train"] = "pociag transport",
        ["Bus"] = "autobus transport", ["Coffee"] = "kawa jedzenie", ["Movie"] = "kino rozrywka"
    };

    public static IReadOnlyList<CategoryIconItem> All { get; } = Enum.GetValues<PackIconMaterialKind>()
        .Where(kind => kind != PackIconMaterialKind.None)
        .Select(kind => new CategoryIconItem($"mdi:{kind}", kind.ToString(), kind,
            Normalize($"{kind} {PolishAliases.GetValueOrDefault(kind.ToString())}")))
        .OrderBy(icon => icon.Name, StringComparer.Ordinal).ToArray();

    public static PackIconMaterialKind Resolve(string? key) =>
        key is not null && key.StartsWith("mdi:", StringComparison.Ordinal)
        && Enum.TryParse<PackIconMaterialKind>(key[4..], out var kind) && Enum.IsDefined(kind)
            ? kind : PackIconMaterialKind.None;

    public static IReadOnlyList<CategoryIconItem> Search(string? query)
    {
        var terms = Normalize(query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return terms.Length == 0 ? All : All.Where(icon => terms.All(term =>
            icon.SearchText.Contains(term, StringComparison.Ordinal))).ToArray();
    }

    private static string Normalize(string text) => new(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(c => c == 'ł' || c == 'Ł' ? 'l' : char.ToLowerInvariant(c)).ToArray());
}
