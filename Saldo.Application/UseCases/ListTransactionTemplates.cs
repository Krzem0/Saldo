using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;

namespace Saldo.Application.UseCases;

public sealed class ListTransactionTemplates(ITransactionTemplateRepository templates)
{
    public async Task<IReadOnlyList<TransactionTemplateDto>> ExecuteAsync(CancellationToken ct = default) =>
        (await templates.GetAllAsync(ct)).Select(TransactionTemplateMapper.ToDto).ToArray();
}
