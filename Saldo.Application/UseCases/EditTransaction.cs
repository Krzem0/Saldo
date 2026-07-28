using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;
using Saldo.Domain.Entities;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FluentValidation;
using Saldo.Application.Validation;

namespace Saldo.Application.UseCases;

public sealed class EditTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly IPartyRepository _parties;
    private readonly ILocationRepository _locations;
    private readonly ILogger<EditTransaction> _logger;
    private readonly IValidator<EditTransactionCommand> _validator;

    public EditTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations)
        : this(transactions, parties, locations, new EditTransactionCommandValidator(), NullLogger<EditTransaction>.Instance)
    {
    }

    public EditTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations, ILogger<EditTransaction> logger)
        : this(transactions, parties, locations, new EditTransactionCommandValidator(), logger)
    {
    }

    public EditTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations, IValidator<EditTransactionCommand> validator, ILogger<EditTransaction> logger)
    {
        _transactions = transactions;
        _parties = parties;
        _locations = locations;
        _logger = logger;
        _validator = validator;
    }

    public async Task<Result<TransactionDto>> ExecuteAsync(EditTransactionCommand command, CancellationToken ct = default)
    {
        _logger.LogDebug("Editing transaction {TransactionId}.", command.Id);

        var validationResult = await _validator.ValidateAsync(command, ct);
        var errors = validationResult.Errors
            .Select(failure => (IError)new Error(failure.ErrorCode)
                .WithMetadata("PropertyName", failure.PropertyName))
            .ToList();

        if (errors.Any(error => error.Message == ErrorCodes.Transaction.IdMustBePositive))
        {
            return Result.Fail<TransactionDto>(errors);
        }

        var existing = await _transactions.GetByIdAsync(command.Id, ct);
        if (existing is null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found for edit.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.NotFound);
        }

        var payer = await ResolvePartyAsync(command.PayerId, command.PayerName, ct);
        if (payer is null)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because payer could not be resolved.", command.Id);
            AddErrorIfMissing(errors, ErrorCodes.Transaction.PayerRequired, nameof(ITransactionCommand.PayerName));
        }

        var counterparty = await ResolvePartyAsync(command.CounterpartyId, command.CounterpartyName, ct);
        if (counterparty is null)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because counterparty could not be resolved.", command.Id);
            AddErrorIfMissing(errors, ErrorCodes.Transaction.CounterpartyRequired, nameof(ITransactionCommand.CounterpartyName));
        }

        var location = await ResolveLocationAsync(command.Location, ct);
        if (!string.IsNullOrWhiteSpace(command.Location) && location is null)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because location could not be resolved.", command.Id);
            AddErrorIfMissing(errors, ErrorCodes.Transaction.LocationInvalid, nameof(ITransactionCommand.Location));
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because validation failed: {ValidationErrors}.",
                command.Id, string.Join(", ", errors.Select(error => error.Message)));
            return Result.Fail<TransactionDto>(errors);
        }

        if (payer is null || counterparty is null)
        {
            throw new InvalidOperationException("Transaction references could not be resolved after validation.");
        }

        existing.Date = command.Date;
        existing.Type = command.Type;
        existing.Amount = command.Amount;
        existing.CategoryId = command.CategoryId;
        existing.PayerId = payer.Id;
        existing.Payer = payer;
        existing.CounterpartyId = counterparty.Id;
        existing.Counterparty = counterparty;
        existing.LocationId = location?.Id;
        existing.Location = location;
        existing.Description = command.Description;
        existing.Tags = command.TagIds
            .Select(tagId => new TransactionTag { TagId = tagId })
            .ToList();

        await _transactions.UpdateAsync(existing, ct);

        var saved = await _transactions.GetByIdAsync(existing.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {existing.Id} not found after update.");

        _logger.LogInformation("Transaction {TransactionId} edited successfully.", saved.Id);

        return Result.Ok(TransactionMapper.ToDto(saved));
    }

    private async Task<Party?> ResolvePartyAsync(int? partyId, string? partyName, CancellationToken ct)
    {
        if (partyId.HasValue && partyId.Value > 0)
        {
            var existingById = await _parties.GetByIdAsync(partyId.Value, ct);
            if (existingById is not null)
            {
                return existingById;
            }
        }

        var normalizedInput = NormalizeName(partyName);
        if (normalizedInput is null)
        {
            return null;
        }

        var existing = (await _parties.GetAllAsync(ct))
            .FirstOrDefault(party => string.Equals(
                NormalizeName(party.Name),
                normalizedInput,
                StringComparison.Ordinal));

        if (existing is not null)
        {
            return existing;
        }

        return null;
    }

    private async Task<Location?> ResolveLocationAsync(string? locationName, CancellationToken ct)
    {
        var normalizedInput = NormalizeName(locationName);
        if (normalizedInput is null)
        {
            return null;
        }

        var existing = (await _locations.GetAllAsync(ct))
            .FirstOrDefault(location => string.Equals(
                NormalizeName(location.Name),
                normalizedInput,
                StringComparison.Ordinal));

        if (existing is not null)
        {
            return existing;
        }

        return null;
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }

    private static void AddErrorIfMissing(List<IError> errors, string errorCode, string propertyName)
    {
        if (errors.Any(error => error.Message == errorCode))
        {
            return;
        }

        errors.Add(new Error(errorCode).WithMetadata("PropertyName", propertyName));
    }
}
