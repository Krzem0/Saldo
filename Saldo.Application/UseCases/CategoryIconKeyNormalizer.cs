using System.Text.RegularExpressions;

namespace Saldo.Application.UseCases;

/// <summary>Validates a portable identifier without depending on a GUI icon library.</summary>
internal static class CategoryIconKeyNormalizer
{
    public static string? Normalize(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;
        var key = iconKey.Trim();
        if (key.Length > 100 || !Regex.IsMatch(key, @"\Amdi:[A-Za-z][A-Za-z0-9]*\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid category icon identifier.", nameof(iconKey));
        return key;
    }
}
