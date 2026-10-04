using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddTag(ITagRepository tags)
{
    public async Task<Tag> ExecuteAsync(string name, string? colorCode = null, CancellationToken ct = default)
    {
        var normalizedColor = ColorCodeNormalizer.Normalize(colorCode);
        var tag = new Tag { Name = await TagNameValidator.NormalizeAsync(tags, name, null, ct), ColorCode = normalizedColor };
        await tags.AddAsync(tag, ct);
        return tag;
    }
}
