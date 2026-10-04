using Saldo.Application.Errors;
using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

internal static class TagNameValidator
{
    public static async Task<string> NormalizeAsync(ITagRepository tags, string name, int? exceptId, CancellationToken ct)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 50)
            throw new InvalidTagNameException();

        if ((await tags.GetAllAsync(ct)).Any(tag => tag.Id != exceptId
            && string.Equals(tag.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new DuplicateReferenceException("tag", normalized);

        return normalized;
    }
}
