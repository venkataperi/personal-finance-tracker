namespace FinanceTracker.Api.Modules.Imports.Services;

public static class StatementCategorizer
{
    public static string CategorizeTransaction(string description, string sourceCategory)
    {
        var descriptionLower = description.ToLowerInvariant();
        var sourceCategoryLower = sourceCategory.ToLowerInvariant();

        return
            sourceCategoryLower.Contains("credit card payment") ||
            descriptionLower.Contains("payment received")
                ? "Credit Card Payment"
            : sourceCategoryLower.Contains("fees") ||
              descriptionLower.Contains("interest")
                ? "Fees / Interest"
            : descriptionLower.Contains("costco") ||
              descriptionLower.Contains("supermarket") ||
              sourceCategoryLower.Contains("grocery")
                ? "Groceries"
            : sourceCategoryLower.Contains("restaurant")
                ? "Restaurants / Coffee"
            : sourceCategoryLower.Contains("car rental") ||
              sourceCategoryLower.Contains("taxi") ||
              descriptionLower.Contains("uber")
                ? "Transportation"
            : sourceCategoryLower.Contains("internet") ||
              sourceCategoryLower.Contains("cable")
                ? "Phone / Internet"
            : sourceCategoryLower.Contains("electronics") ||
              descriptionLower.Contains("openai")
                ? "Software / Subscriptions"
            : sourceCategoryLower.Contains("gift") ||
              sourceCategoryLower.Contains("donation")
                ? "Gifts / Donations"
            : sourceCategoryLower.Contains("alcohol") ||
              sourceCategoryLower.Contains("bar")
                ? "Entertainment"
            : sourceCategoryLower.Contains("insurance") ||
              sourceCategoryLower.Contains("finance")
                ? "Insurance / Financial"
            : sourceCategoryLower.Contains("shopping")
                ? "Shopping"
            : "Miscellaneous";
    }
}