using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;
using Saldo.Domain.Entities;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Saldo.Application.UseCases;

public sealed class EditTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly IPartyRepository _parties;
    private readonly ILocationRepository _locations;
    private readonly ILogger<EditTransaction> _logger;

    public EditTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations)
        : this(transactions, parties, locations, NullLogger<EditTransaction>.Instance)
    {
    }

    public EditTransaction(ITransactionRepository transactions, IPartyRepository parties, ILocationRepository locations, ILogger<EditTransaction> logger)
    {
        _transactions = transactions;
        _parties = parties;
        _locations = locations;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> ExecuteAsync(EditTransactionCommand command, CancellationToken ct = default)
    {
        _logger.LogDebug("Editing transaction {TransactionId}.", command.Id);

        if (command.Id <= 0)
        {
            _logger.LogWarning("Transaction edit rejected because id must be positive.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.IdMustBePositive);
        }
        if (command.Amount <= 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because amount must be positive.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.AmountMustBePositive);
        }
        if (command.CategoryId <= 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because category id is missing.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CategoryRequired);
        }
        if (!command.PayerId.HasValue && string.IsNullOrWhiteSpace(command.PayerName))
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because payer is missing.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.PayerRequired);
        }
        if (!command.CounterpartyId.HasValue && string.IsNullOrWhiteSpace(command.CounterpartyName))
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because counterparty is missing.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CounterpartyRequired);
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
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.PayerRequired);
        }

        var counterparty = await ResolvePartyAsync(command.CounterpartyId, command.CounterpartyName, ct);
        if (counterparty is null)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because counterparty could not be resolved.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CounterpartyRequired);
        }

        existing.Date = command.Date;
        existing.Type = command.Type;
        existing.Amount = command.Amount;
        existing.CategoryId = command.CategoryId;
        existing.PayerId = payer.Id;
        existing.Payer = payer;
        existing.CounterpartyId = counterparty.Id;
        existing.Counterparty = counterparty;
        var location = await ResolveLocationAsync(command.Location, ct);
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
