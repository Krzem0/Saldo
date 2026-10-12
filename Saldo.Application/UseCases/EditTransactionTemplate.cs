using FluentResults;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;

namespace Saldo.Application.UseCases;

public sealed class EditTransactionTemplate(ITransactionTemplateRepository templates, TransactionTemplateInput input)
{
    public async Task<Result<TransactionTemplateDto>> ExecuteAsync(int id, SaveTransactionTemplateCommand command, CancellationToken ct = default)
    {
        if (id <= 0) return Result.Fail<TransactionTemplateDto>("TransactionTemplate.IdMustBePositive");
        if (await templates.GetByIdAsync(id, ct) is null) return Result.Fail<TransactionTemplateDto>("TransactionTemplate.NotFound");
        var prepared = await input.PrepareAsync(command, id, ct);
        if (prepared.IsFailed) return Result.Fail<TransactionTemplateDto>(prepared.Errors);
        await templates.UpdateAsync(prepared.Value, ct);
        return Result.Ok(TransactionTemplateMapper.ToDto(prepared.Value));
    }
}
