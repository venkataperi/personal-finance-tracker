namespace FinanceTracker.Api.Modules.Imports;

public class ImportSummaryResponse
{
    public decimal TotalExpenses { get; set; }

    public decimal TotalPaymentsOrRefunds { get; set; }

    public decimal NetActivity { get; set; }

    public int TransactionCount { get; set; }

    public List<ImportCategoryTotal> CategoryTotals { get; set; } = [];
}

public class ImportCategoryTotal
{
    public string Category { get; set; } = string.Empty;

    public decimal Total { get; set; }
}