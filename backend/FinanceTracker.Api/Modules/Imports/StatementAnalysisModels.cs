namespace FinanceTracker.Api.Modules.Imports;

public class StatementAnalysisResponse
{
    public string FileName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Institution { get; set; } = "Unknown";
    public string ProductName { get; set; } = string.Empty;
    public string StatementKind { get; set; } = "CreditCard";
    public decimal TotalExpenses { get; set; }
    public decimal TotalPaymentsOrRefunds { get; set; }
    public decimal NetActivity { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal FeesPaid { get; set; }
    public decimal NewBalance { get; set; }
    public decimal MinimumPaymentDue { get; set; }
    public string? PaymentDueDate { get; set; }
    public int? PointsEarned { get; set; }
    public int? PreviousPointsBalance { get; set; }
    public int? NewPointsBalance { get; set; }
    public decimal? CashBackEarned { get; set; }
    public decimal? CashBackRedeemed { get; set; }
    public decimal? CashBackBalance { get; set; }
    public decimal EstimatedRewardValue { get; set; }
    public decimal EffectiveRewardRate { get; set; }
    public decimal? PurchaseInterestRate { get; set; }
    public decimal? CashAdvanceInterestRate { get; set; }
    public string RewardSummary { get; set; } = string.Empty;
    public string PaymentPriority { get; set; } = "Review";
    public string PaymentPriorityReason { get; set; } = string.Empty;
    public List<ImportCategoryTotal> CategoryTotals { get; set; } = [];
    public List<ImportPreviewTransaction> Transactions { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public class MultiStatementComparisonResponse
{
    public List<StatementAnalysisResponse> Statements { get; set; } = [];
    public List<CategoryRecommendationResponse> CategoryRecommendations { get; set; } = [];
    public List<CategoryExpenseAdviceResponse> CategoryExpenseAdvice { get; set; } = [];
    public List<PaymentPriorityResponse> PaymentPriorities { get; set; } = [];
}

public class CategoryRecommendationResponse
{
    public string Category { get; set; } = string.Empty;
    public string RecommendedAccount { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}


public class CategoryExpenseAdviceResponse
{
    public string Category { get; set; } = string.Empty;
    public decimal Expense { get; set; }
    public decimal EstimatedPointsOrCashBackValue { get; set; }
    public string PointsOrCashBack { get; set; } = string.Empty;
    public string CardUsed { get; set; } = string.Empty;
    public string AdvisedCardToUse { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class PaymentPriorityResponse
{
    public string AccountName { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public decimal NewBalance { get; set; }
    public decimal MinimumPaymentDue { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal? PurchaseInterestRate { get; set; }
    public decimal? CashAdvanceInterestRate { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
