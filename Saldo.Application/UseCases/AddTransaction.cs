using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;
using Saldo.Domain.Entities;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Saldo.Application.UseCases;

public sealed class AddTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly IPartyRepository _parties;
    private readonly ILocationRepository _locations;
    private readonly ILogger<AddTransaction> _logger;

    public AddTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations)
        : this(transactions, parties, locations, NullLogger<AddTransaction>.Instance)
    {
    }

    public AddTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations, ILogger<AddTransaction> logger)
    {
        _transactions = transactions;
        _parties = parties;
        _locations = locations;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> ExecuteAsync(AddTransactionCommand command, CancellationToken ct = default)
    {
        _logger.LogDebug("Adding transaction for {Date} with amount {Amount}.", command.Date, command.Amount);

        if (command.Amount <= 0)
        {
            _logger.LogWarning("Transaction rejected because amount must be positive.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.AmountMustBePositive);
        }
        if (command.CategoryId <= 0)
        {
            _logger.LogWarning("Transaction rejected because category id is missing.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CategoryRequired);
        }
        if (!command.PayerId.HasValue && string.IsNullOrWhiteSpace(command.PayerName))
        {
            _logger.LogWarning("Transaction rejected because payer is missing.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.PayerRequired);
        }
        if (!command.CounterpartyId.HasValue && string.IsNullOrWhiteSpace(command.CounterpartyName))
        {
            _logger.LogWarning("Transaction rejected because counterparty is missing.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CounterpartyRequired);
        }

        var payer = await ResolvePartyAsync(command.PayerId, command.PayerName, ct);
        if (payer is null)
        {
            _logger.LogWarning("Transaction rejected because payer could not be resolved.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.PayerRequired);
        }

        var counterparty = await ResolvePartyAsync(command.CounterpartyId, command.CounterpartyName, ct);
        if (counterparty is null)
        {
            _logger.LogWarning("Transaction rejected because counterparty could not be resolved.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CounterpartyRequired);
        }

        var location = await ResolveLocationAsync(command.Location, ct);

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

        var party = new Party { Name = partyName!.Trim() };
        await _parties.AddAsync(party, ct);
        return party;
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

        var location = new Location { Name = locationName!.Trim() };
        await _locations.AddAsync(location, ct);
        return location;
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
}
