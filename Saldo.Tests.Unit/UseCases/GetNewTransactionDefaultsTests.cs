using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class GetNewTransactionDefaultsTests
{
    [Fact]
    public async Task ExecuteAsync_WhenJaExists_UsesJaAsDefaultPayer()
    {
        var useCase = new GetNewTransactionDefaults(new FakePartyRepository(
        [
            new Party { Id = 2, Name = "Mama" },
            new Party { Id = 7, Name = "Ja" }
        ]));

        var result = await useCase.ExecuteAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), result.Date);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal(7, result.PayerId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMeExists_UsesMeAsDefaultPayer()
    {
        var useCase = new GetNewTransactionDefaults(new FakePartyRepository(
        [
            new Party { Id = 3, Name = "Shop" },
            new Party { Id = 5, Name = "Me" }
        ]));

        var result = await useCase.ExecuteAsync();

        Assert.Equal(5, result.PayerId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSelfPartyDoesNotExist_FallsBackToFirstAvailableParty()
    {
        var useCase = new GetNewTransactionDefaults(new FakePartyRepository(
        [
            new Party { Id = 11, Name = "Adam" },
            new Party { Id = 12, Name = "Zofia" }
        ]));

        var result = await useCase.ExecuteAsync();

        Assert.Equal(11, result.PayerId);
    }
}
