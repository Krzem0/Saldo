using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.ValueObjects;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class VerifyAccountingPeriodTests
{
    [Fact]
    public async Task OpenMonth_DoesNotRequestTransactionScan()
    {
        var result = await new VerifyAccountingPeriod(new FailingReadRepository(isClosed: false)).ExecuteAsync(2026, 1);
        Assert.True(result.IsSuccess);
        Assert.Equal(AccountingPeriodVerificationStatus.Open, result.Value.Status);
        Assert.Null(result.Value.ActualSnapshot);
    }

    [Fact]
    public async Task FailedConsistentRead_ReturnsFailureInsteadOfValidOrMismatch()
    {
        var result = await new VerifyAccountingPeriod(new FailingReadRepository()).ExecuteAsync(2026, 1);
        Assert.True(result.IsFailed);
        Assert.Equal(ErrorCodes.AccountingPeriod.VerificationFailed, Assert.Single(result.Errors).Message);
    }

    [Fact]
    public async Task CancelledRead_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new VerifyAccountingPeriod(new FailingReadRepository()).ExecuteAsync(2026, 1, cancellation.Token));
    }

    [Theory]
    [InlineData("missing", AccountingPeriodVerificationStatus.MissingSnapshot)]
    [InlineData("unsupported", AccountingPeriodVerificationStatus.UnsupportedHashVersion)]
    public async Task IncompleteOrUnsupportedSnapshot_IsNeverReportedAsValid(string kind, AccountingPeriodVerificationStatus status)
    {
        var periods = new FakeAccountingPeriodRepository();
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1);
        var period = (await periods.GetByMonthAsync(2026, 1))!;
        if (kind == "missing") period.ClosedTransactionCount = null;
        else period.HashVersion = 999;

        var result = await new VerifyAccountingPeriod(periods).ExecuteAsync(2026, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, result.Value.Status);
        Assert.NotNull(result.Value.ActualSnapshot);
        Assert.Equal(kind == "missing" ? 1 : 999, period.HashVersion);
        if (kind == "missing") Assert.Null(period.ClosedTransactionCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2026, 13)]
    [InlineData(9999, 12)]
    public async Task InvalidMonth_ReturnsValidationFailure(int year, int month)
    {
        var result = await new VerifyAccountingPeriod(new FailingReadRepository()).ExecuteAsync(year, month);
        Assert.Equal(ErrorCodes.AccountingPeriod.InvalidMonth, Assert.Single(result.Errors).Message);
    }

    private sealed class FailingReadRepository(bool isClosed = true) : IAccountingPeriodRepository
    {
        public Task<AccountingPeriod?> GetByMonthAsync(int year, int month, CancellationToken ct = default) =>
            Task.FromResult<AccountingPeriod?>(new AccountingPeriod { IsClosed = isClosed });
        public Task SetClosedAsync(CalendarMonth month, bool closed, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<AccountingPeriodVerificationData> ReadVerificationDataAsync(CalendarMonth month, CancellationToken ct = default) =>
            throw new IOException("Simulated read failure.");
    }
}
