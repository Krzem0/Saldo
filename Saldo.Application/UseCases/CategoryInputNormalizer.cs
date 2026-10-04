namespace Saldo.Application.UseCases;

internal static class CategoryInputNormalizer
{
    public static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name cannot be empty.", nameof(name))
        : name.Trim();

    public static string? NormalizeColorCode(string? colorCode) => ColorCodeNormalizer.Normalize(colorCode);
}
