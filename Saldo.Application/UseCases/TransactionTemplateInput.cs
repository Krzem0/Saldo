using FluentResults;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Validation;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class TransactionTemplateInput(
    ITransactionTemplateRepository templates, ICategoryRepository categories,
    IPartyRepository parties, ILocationRepository locations, ITagRepository tags)
{
    internal async Task<Result<TransactionTemplate>> PrepareAsync(SaveTransactionTemplateCommand command, int? id, CancellationToken ct)
    {
        var validation = await new SaveTransactionTemplateCommandValidator().ValidateAsync(command, ct);
        var errors = validation.Errors.Select(f => (IError)new Error(f.ErrorCode)
            .WithMetadata("PropertyName", f.PropertyName)).ToList();
        if (errors.Count > 0) return Result.Fail<TransactionTemplate>(errors);

        void Invalid(string field, string code) => errors.Add(new Error($"TransactionTemplate.{code}").WithMetadata("PropertyName", field));
        var name = command.Name.Trim();
        var existing = await templates.GetAllAsync(ct);
        var duplicateName = existing.Any(t => t.Id != id && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (duplicateName)
            Invalid(nameof(command.Name), "NameDuplicate");
        if (command.CategoryId is int category && await categories.GetByIdAsync(category, ct) is null) Invalid(nameof(command.CategoryId), "CategoryInvalid");
        if (command.PayerId is int payer && await parties.GetByIdAsync(payer, ct) is null) Invalid(nameof(command.PayerId), "PayerInvalid");
        if (command.CounterpartyId is int counterparty && await parties.GetByIdAsync(counterparty, ct) is null) Invalid(nameof(command.CounterpartyId), "CounterpartyInvalid");
        if (command.LocationId is int location && await locations.GetByIdAsync(location, ct) is null) Invalid(nameof(command.LocationId), "LocationInvalid");
        var tagIds = (command.TagIds ?? []).Distinct().ToArray();
        foreach (var tagId in tagIds)
            if (await tags.GetByIdAsync(tagId, ct) is null) Invalid(nameof(command.TagIds), "TagInvalid");
        var description = NormalizeDescription(command.Description);
        var tagSet = tagIds.ToHashSet();
        if (!duplicateName && existing.Any(t => t.Id != id && t.TransactionType == command.TransactionType
                && string.Equals(NormalizeDescription(t.Description), description, StringComparison.OrdinalIgnoreCase)
                && t.Amount == command.Amount && t.CategoryId == command.CategoryId
                && t.PayerId == command.PayerId && t.CounterpartyId == command.CounterpartyId
                && t.LocationId == command.LocationId && tagSet.SetEquals(t.Tags.Select(link => link.TagId))))
            Invalid(string.Empty, "ContentDuplicate");
        if (errors.Count > 0) return Result.Fail<TransactionTemplate>(errors);

        return Result.Ok(new TransactionTemplate
        {
            Id = id ?? 0, Name = name, TransactionType = command.TransactionType,
            Description = description, CategoryId = command.CategoryId,
            PayerId = command.PayerId, CounterpartyId = command.CounterpartyId,
            LocationId = command.LocationId, Amount = command.Amount,
            Tags = tagIds.Select(tagId => new TransactionTemplateTag { TagId = tagId }).ToList()
        });
    }

    private static string? NormalizeDescription(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
