using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class SetDefaultPayer(ITransactionSettingsRepository settings, IPartyRepository parties)
{
    public async Task ExecuteAsync(int? payerId, CancellationToken ct = default)
    {
        if (payerId is int id && await parties.GetByIdAsync(id, ct) is null)
            throw new ArgumentException("The default payer must refer to an existing party.", nameof(payerId));
        var current = await settings.GetAsync(ct);
        current.DefaultPayerId = payerId;
        await settings.SaveAsync(current, ct);
    }
}
