using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddParty
{
    private readonly IPartyRepository _parties;

    public AddParty(IPartyRepository parties) => _parties = parties;

    public async Task<Party> ExecuteAsync(string name, CancellationToken ct = default)
    {
        var party = new Party { Name = NormalizeName(name) };
        await _parties.AddAsync(party, ct);
        return party;
    }

    private static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name cannot be empty.", nameof(name))
        : name.Trim();
}
