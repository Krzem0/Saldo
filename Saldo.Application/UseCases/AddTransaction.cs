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

public sealed class AddTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly IPartyRepository _parties;
    private readonly ILocationRepository _locations;
    private readonly ILogger<AddTransaction> _logger;
    private readonly IValidator<AddTransactionCommand> _validator;

    public AddTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations)
        : this(transactions, parties, locations, new AddTransactionCommandValidator(), NullLogger<AddTransaction>.Instance)
    {
    }

    public AddTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations, ILogger<AddTransaction> logger)
        : this(transactions, parties, locations, new AddTransactionCommandValidator(), logger)
    {
    }

    public AddTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations, IValidator<AddTransactionCommand> validator, ILogger<AddTransaction> logger)
    {
        _transactions = transactions;
        _parties = parties;
        _locations = locations;
        _logger = logger;
        _validator = validator;
    }

    public async Task<Result<TransactionDto>> ExecuteAsync(AddTransactionCommand command, CancellationToken ct = default)
    {
        _logger.LogDebug("Adding transaction for {Date} with amount {Amount}.", command.Date, command.Amount);

        var validationResult = await _validator.ValidateAsync(command, ct);
        var errors = validationResult.Errors
            .Select(failure => (IError)new Error(failure.ErrorCode)
                .WithMetadata("PropertyName", failure.PropertyName))
            .ToList();

        var payer = await ResolvePartyAsync(command.PayerId, command.PayerName, ct);
        if (payer is null)
        {
            _logger.LogWarning("Transaction rejected because payer could not be resolved.");
            AddErrorIfMissing(errors, ErrorCodes.Transaction.PayerRequired, nameof(ITransactionCommand.PayerName));
        }

        var counterparty = await ResolvePartyAsync(command.CounterpartyId, command.CounterpartyName, ct);
        if (counterparty is null)
        {
            _logger.LogWarning("Transaction rejected because counterparty could not be resolved.");
            AddErrorIfMissing(errors, ErrorCodes.Transaction.CounterpartyRequired, nameof(ITransactionCommand.CounterpartyName));
        }

        var location = await ResolveLocationAsync(command.Location, ct);
        if (!string.IsNullOrWhiteSpace(command.Location) && location is null)
        {
            _logger.LogWarning("Transaction rejected because location could not be resolved.");
            AddErrorIfMissing(errors, ErrorCodes.Transaction.LocationInvalid, nameof(ITransactionCommand.Location));
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Transaction rejected because validation failed: {ValidationErrors}.",
                string.Join(", ", errors.Select(error => error.Message)));
            return Result.Fail<TransactionDto>(errors);
        }

        if (payer is null || counterparty is null)
        {
            throw new InvalidOperationException("Transaction references could not be resolved after validation.");
        }

        var transaction = new Transaction
        {
            Date = command.Date,
            Type = command.Type,
            Amount = command.Amount,
            CategoryId = command.CategoryId,
            PayerId = payer.Id,
            Payer = payer,
            CounterpartyId = counterparty.Id,
            Counterparty = counterparty,
            LocationId = location?.Id,
            Location = location,
            Description = command.Description,
            Tags = command.TagIds
                .Select(tagId => new TransactionTag { TagId = tagId })
                .ToList()
        };

        await _transactions.AddAsync(transaction, ct);

        var saved = await _transactions.GetByIdAsync(transaction.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {transaction.Id} not found after insert.");

        _logger.LogInformation("Transaction {TransactionId} added successfully.", saved.Id);

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
