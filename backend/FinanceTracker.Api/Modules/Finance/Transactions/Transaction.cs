namespace FinanceTracker.Api.Modules.Finance.Transactions;

public class Transaction
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public decimal Amount { get; set; }

    public string Type { get; set; } = string.Empty;

    public DateOnly TransactionDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}