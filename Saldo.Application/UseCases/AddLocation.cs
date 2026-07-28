using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddLocation
{
    private readonly ILocationRepository _locations;

    public AddLocation(ILocationRepository locations) => _locations = locations;

    public async Task<Location> ExecuteAsync(string name, CancellationToken ct = default)
    {
        var normalizedName = NormalizeName(name);
        if ((await _locations.GetAllAsync(ct)).Any(location => string.Equals(location.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateReferenceException("location", normalizedName);
        }

        var location = new Location { Name = normalizedName };
        await _locations.AddAsync(location, ct);
        return location;
    }

    private static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name cannot be empty.", nameof(name))
        : name.Trim();
}
