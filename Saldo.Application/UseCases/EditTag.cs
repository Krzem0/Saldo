using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class EditTag(ITagRepository tags)
{
    public async Task ExecuteAsync(int id, string name, string? colorCode = null, CancellationToken ct = default)
    {
        var normalized = await TagNameValidator.NormalizeAsync(tags, name, id, ct);
        var normalizedColor = ColorCodeNormalizer.Normalize(colorCode);
        var tag = await tags.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Tag {id} not found.");
        tag.Name = normalized;
        tag.ColorCode = normalizedColor;
        await tags.UpdateAsync(tag, ct);
    }
}
