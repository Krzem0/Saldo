using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Unit.UseCases;

public sealed class GetNewTransactionDefaultsTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(null)]
    public async Task ExecuteAsync_UsesStoredPayerIdWithoutNameOrAlphabeticalFallback(int? payerId)
    {
        var useCase = new GetNewTransactionDefaults(new SettingsRepository(payerId));
        var result = await useCase.ExecuteAsync();
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), result.Date);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal(payerId, result.PayerId);
    }

    private sealed class SettingsRepository(int? payerId) : ITransactionSettingsRepository
    {
        public Task<TransactionSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new TransactionSettings { DefaultPayerId = payerId });
        public Task SaveAsync(TransactionSettings settings, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
