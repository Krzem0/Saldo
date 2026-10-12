using Saldo.Domain.Enums;

namespace Saldo.Domain.Entities;

public sealed class TransactionTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? PayerId { get; set; }
    public Party? Payer { get; set; }
    public int? CounterpartyId { get; set; }
    public Party? Counterparty { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public ICollection<TransactionTemplateTag> Tags { get; set; } = new List<TransactionTemplateTag>();
}
