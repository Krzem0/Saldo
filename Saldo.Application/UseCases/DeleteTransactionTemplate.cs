using FluentResults;
using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class DeleteTransactionTemplate(ITransactionTemplateRepository templates)
{
    public async Task<Result> ExecuteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return Result.Fail("TransactionTemplate.IdMustBePositive");
        if (await templates.GetByIdAsync(id, ct) is null) return Result.Fail("TransactionTemplate.NotFound");
        await templates.DeleteAsync(id, ct);
        return Result.Ok();
    }
}
