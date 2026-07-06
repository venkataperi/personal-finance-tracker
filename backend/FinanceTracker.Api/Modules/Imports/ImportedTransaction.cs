namespace FinanceTracker.Api.Modules.Imports;

public class ImportedTransaction
{
    public Guid Id { get; set; }

    public StatementType StatementType { get; set; }

    public DateOnly TransactionDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? SourceCategory { get; set; }

    public decimal RawAmount { get; set; }

    public string TransactionGroup { get; set; } = string.Empty;

    public string AppCategory { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}