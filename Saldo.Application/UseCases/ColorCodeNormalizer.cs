namespace Saldo.Application.UseCases;

internal static class ColorCodeNormalizer
{
    public static string? Normalize(string? colorCode)
    {
        if (string.IsNullOrWhiteSpace(colorCode)) return null;
        var normalized = colorCode.Trim().ToUpperInvariant();
        return normalized.Length == 7 && normalized[0] == '#'
            && normalized[1..].All(Uri.IsHexDigit)
            ? normalized
            : throw new ArgumentException("Color code must use the #RRGGBB format.", nameof(colorCode));
    }
}
