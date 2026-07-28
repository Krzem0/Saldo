using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

/// <summary>
/// Temporary presentation state of an unfinished transaction form.
/// It is intentionally kept in WPF and is not persisted or sent to Application.
/// </summary>
public sealed class TransactionDraft
{
    public int? TransactionId { get; init; }
    public DateTime Date { get; init; }
    public TransactionType Type { get; init; }
    public string AmountText { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string CategoryText { get; init; } = string.Empty;
    public int? PayerId { get; init; }
    public string PayerText { get; init; } = string.Empty;
    public int? CounterpartyId { get; init; }
    public string CounterpartyText { get; init; } = string.Empty;
    public int? LocationId { get; init; }
    public string LocationText { get; init; } = string.Empty;
    public string? Description { get; init; }
}
