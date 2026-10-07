using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class SetTransactionDefaults(ITransactionSettingsRepository settings, IPartyRepository parties, ILocationRepository locations)
{
    public async Task ExecuteAsync(int? payerId, int? locationId, CancellationToken ct = default)
    {
        if (payerId is int partyId && await parties.GetByIdAsync(partyId, ct) is null)
            throw new ArgumentException("The default payer must refer to an existing party.", nameof(payerId));
        if (locationId is int placeId && await locations.GetByIdAsync(placeId, ct) is null)
            throw new ArgumentException("The default location must refer to an existing location.", nameof(locationId));
        var current = await settings.GetAsync(ct);
        current.DefaultPayerId = payerId;
        current.DefaultLocationId = locationId;
        await settings.SaveAsync(current, ct);
    }
}
