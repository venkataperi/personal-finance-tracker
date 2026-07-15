using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceTracker.Api.Modules.Imports.Services;

public static class PdfStatementParser
{
    public static List<ImportPreviewTransaction> ParsePdfTransactions(
        string pdfText,
        StatementType statementType)
    {
        var transactions = new List<ImportPreviewTransaction>();

        var pattern =
            @"(?<date>\d{4}-\d{2}-\d{2})" +
            @"(?<card>\*{12}\d{4})" +
            @"(?<rest>.*?)(?=\d{4}-\d{2}-\d{2}\*{12}\d{4}|$)";

        var matches = Regex.Matches(pdfText, pattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var dateText = match.Groups["date"].Value;
            var rest = match.Groups["rest"].Value;

            if (!DateOnly.TryParse(dateText, out var transactionDate))
            {
                continue;
            }

            var amountMatch = Regex.Match(
                rest,
                @"(?<amount>\d+\.\d{1,2})(?<trailingZero>0|01)?$");

            if (!amountMatch.Success)
            {
                continue;
            }

            var amountText = amountMatch.Groups["amount"].Value;
            var descriptionAndCategory = rest[..amountMatch.Index];

            var isPaymentOrRefund =
                descriptionAndCategory.Contains("Payment received", StringComparison.OrdinalIgnoreCase) ||
                descriptionAndCategory.Contains("Credit card payment", StringComparison.OrdinalIgnoreCase);

            decimal.TryParse(
                amountText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var rawAmount);

            if (rawAmount <= 0)
            {
                continue;
            }

            var knownCategories = new[]
            {
                "Car rentals & taxis",
                "Gifts and donations",
                "Credit card payment",
                "Electronics and software",
                "Alcohol/bars",
                "Restaurants",
                "Internet",
                "Shopping"
            };

            var sourceCategory = "Unknown";
            var description = descriptionAndCategory;

            foreach (var knownCategory in knownCategories)
            {
                var categoryIndex = descriptionAndCategory.LastIndexOf(
                    knownCategory,
                    StringComparison.OrdinalIgnoreCase);

                if (categoryIndex >= 0)
                {
                    description = descriptionAndCategory[..categoryIndex];
                    sourceCategory = descriptionAndCategory[categoryIndex..];
                    break;
                }
            }

            description = description.Trim();
            sourceCategory = sourceCategory.Trim();

            var transactionGroup = statementType == StatementType.CreditCard
                ? isPaymentOrRefund ? "PaymentOrRefund" : "Expense"
                : "Unclassified";

            var appCategory = StatementCategorizer.CategorizeTransaction(description, sourceCategory);

            transactions.Add(new ImportPreviewTransaction
            {
                TransactionDate = transactionDate,
                Description = description,
                SourceCategory = sourceCategory,
                RawAmount = rawAmount,
                TransactionGroup = transactionGroup,
                AppCategory = appCategory
            });
        }

        return transactions;
    }
}