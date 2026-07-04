namespace FinanceTracker.Api.Modules.Finance.Budgets;

public class Budget
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal LimitAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}