using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Unit.UseCases;

public sealed class GetNewTransactionDefaultsTests
{
    [Theory]
    [InlineData(7, 8)]
    [InlineData(null, null)]
    public async Task ExecuteAsync_UsesStoredIdsWithoutNameOrAlphabeticalFallback(int? payerId, int? locationId)
    {
        var useCase = new GetNewTransactionDefaults(new SettingsRepository(payerId, locationId));
        var result = await useCase.ExecuteAsync();
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), result.Date);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal(payerId, result.PayerId);
        Assert.Equal(locationId, result.LocationId);
    }

    private sealed class SettingsRepository(int? payerId, int? locationId) : ITransactionSettingsRepository
    {
        public Task<TransactionSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new TransactionSettings { DefaultPayerId = payerId, DefaultLocationId = locationId });
        public Task SaveAsync(TransactionSettings settings, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
