using FluentResults;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;

namespace Saldo.Application.UseCases;

public sealed class AddTransactionTemplate(ITransactionTemplateRepository templates, TransactionTemplateInput input)
{
    public async Task<Result<TransactionTemplateDto>> ExecuteAsync(SaveTransactionTemplateCommand command, CancellationToken ct = default)
    {
        var prepared = await input.PrepareAsync(command, null, ct);
        if (prepared.IsFailed) return Result.Fail<TransactionTemplateDto>(prepared.Errors);
        await templates.AddAsync(prepared.Value, ct);
        return Result.Ok(TransactionTemplateMapper.ToDto(prepared.Value));
    }
}
