using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class SetDefaultLocation(ITransactionSettingsRepository settings, ILocationRepository locations)
{
    public async Task ExecuteAsync(int? locationId, CancellationToken ct = default)
    {
        if (locationId is int id && await locations.GetByIdAsync(id, ct) is null)
            throw new ArgumentException("The default location must refer to an existing location.", nameof(locationId));
        var current = await settings.GetAsync(ct);
        current.DefaultLocationId = locationId;
        await settings.SaveAsync(current, ct);
    }
}
