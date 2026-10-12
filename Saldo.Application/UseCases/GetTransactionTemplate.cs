using FluentResults;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;

namespace Saldo.Application.UseCases;

public sealed class GetTransactionTemplate(ITransactionTemplateRepository templates)
{
    public async Task<Result<TransactionTemplateDto>> ExecuteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return Result.Fail<TransactionTemplateDto>("TransactionTemplate.IdMustBePositive");
        var template = await templates.GetByIdAsync(id, ct);
        return template is null ? Result.Fail<TransactionTemplateDto>("TransactionTemplate.NotFound")
            : Result.Ok(TransactionTemplateMapper.ToDto(template));
    }
}
