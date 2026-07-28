using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddLocation
{
    private readonly ILocationRepository _locations;

    public AddLocation(ILocationRepository locations) => _locations = locations;

    public async Task<Location> ExecuteAsync(string name, CancellationToken ct = default)
    {
        var location = new Location { Name = NormalizeName(name) };
        await _locations.AddAsync(location, ct);
        return location;
    }

    private static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name cannot be empty.", nameof(name))
        : name.Trim();
}
