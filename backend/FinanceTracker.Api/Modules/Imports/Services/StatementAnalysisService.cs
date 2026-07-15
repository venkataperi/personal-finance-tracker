using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace FinanceTracker.Api.Modules.Imports.Services;

public static class StatementAnalysisService
{
    private const decimal AssumedNationalBankPointValue = 0.01m;

    public static async Task<StatementAnalysisResponse> AnalyzeUploadedStatementAsync(IFormFile file, StatementType statementType)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension == ".pdf")
        {
            var text = await ExtractPdfTextAsync(file);
            return AnalyzePdfText(file.FileName, text, statementType);
        }

        if (extension == ".csv")
        {
            return await AnalyzeCsvAsync(file, statementType);
        }

        return new StatementAnalysisResponse
        {
            FileName = file.FileName,
            AccountName = Path.GetFileNameWithoutExtension(file.FileName),
            StatementKind = statementType.ToString(),
            Notes = [$"Unsupported file type '{extension}'. Upload CSV or PDF statements."]
        };
    }

    public static StatementAnalysisResponse AnalyzePdfText(string fileName, string text, StatementType statementType)
    {
        if (text.Contains("Scotia Momentum", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Scotiabank", StringComparison.OrdinalIgnoreCase))
        {
            return AnalyzeScotiaMomentum(fileName, text, statementType);
        }

        if (text.Contains("BANQUE NATIONALE", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("National Bank", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("A LA CARTE REWARDS", StringComparison.OrdinalIgnoreCase))
        {
            return AnalyzeNationalBankMastercard(fileName, text, statementType);
        }

        var transactions = PdfStatementParser.ParsePdfTransactions(text, statementType);
        return BuildAnalysis(fileName, Path.GetFileNameWithoutExtension(fileName), "Unknown", statementType.ToString(), transactions, 0, 0, 0, 0, 0, null, null, null, null, null, null, null,
            ["Generic PDF parser was used. This format may need a bank-specific parser for better accuracy."]);
    }

    public static MultiStatementComparisonResponse BuildComparison(List<StatementAnalysisResponse> statements)
    {
        ApplyPaymentPriorities(statements);

        var categories = statements
            .SelectMany(statement => statement.CategoryTotals.Select(category => category.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        var recommendations = new List<CategoryRecommendationResponse>();

        foreach (var category in categories)
        {
            var candidates = statements
                .Select(statement => new
                {
                    Statement = statement,
                    Rate = EstimateRewardRate(statement, category),
                    Spend = statement.CategoryTotals
                        .Where(total => total.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                        .Sum(total => total.Total)
                })
                .OrderByDescending(candidate => candidate.Rate)
                .ThenByDescending(candidate => candidate.Spend)
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            var winner = candidates.First();
            var runnerUp = candidates.Skip(1).FirstOrDefault();
            var estimatedValuePerHundred = winner.Rate * 100m;
            var rewardType = GetRewardType(winner.Statement);
            var comparisonText = runnerUp is null
                ? string.Empty
                : $" Compared with {runnerUp.Statement.AccountName} at about {(runnerUp.Rate * 100):0.##}% estimated value.";

            recommendations.Add(new CategoryRecommendationResponse
            {
                Category = category,
                RecommendedAccount = winner.Statement.AccountName,
                Reason = winner.Rate > 0
                    ? $"Use {winner.Statement.AccountName} for {category}. It gives about {estimatedValuePerHundred:0.##}% estimated {rewardType} value, or about {estimatedValuePerHundred:C} per $100 spent.{comparisonText}"
                    : "No clear reward advantage was found. Use the card with lower interest balance or better cash-flow timing."
            });
        }

        return new MultiStatementComparisonResponse
        {
            Statements = statements,
            CategoryRecommendations = recommendations,
            CategoryExpenseAdvice = BuildCategoryExpenseAdvice(statements, categories),
            PaymentPriorities = statements
                .OrderBy(statement => statement.PaymentPriority == "Pay First" ? 0 : statement.PaymentPriority == "Pay Next" ? 1 : 2)
                .Select(statement => new PaymentPriorityResponse
                {
                    AccountName = statement.AccountName,
                    Institution = statement.Institution,
                    NewBalance = statement.NewBalance,
                    MinimumPaymentDue = statement.MinimumPaymentDue,
                    InterestPaid = statement.InterestPaid,
                    PurchaseInterestRate = statement.PurchaseInterestRate,
                    CashAdvanceInterestRate = statement.CashAdvanceInterestRate,
                    Priority = statement.PaymentPriority,
                    Reason = statement.PaymentPriorityReason
                })
                .ToList()
        };
    }

    private static async Task<string> ExtractPdfTextAsync(IFormFile file)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var parts = new List<string>();
        using var document = PdfDocument.Open(memoryStream);
        foreach (var page in document.GetPages())
        {
            parts.Add(page.Text);
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static StatementAnalysisResponse AnalyzeNationalBankMastercard(string fileName, string text, StatementType statementType)
    {
        var statementYear = ExtractNationalBankStatementYear(text);
        var statementMonth = ExtractNationalBankStatementMonth(text);
        var transactions = ParseNationalBankTransactionsFromLines(text, statementType, statementYear, statementMonth);

        var interestPaid = ExtractNationalBankFinanceCharge(text)
            ?? ExtractNationalBankSummaryAmount(text, SummaryAmountType.Interest)
            ?? 0m;
        var headerAmounts = ExtractNationalBankHeaderAmounts(text);
        var newBalance = headerAmounts.NewBalance
            ?? ExtractNationalBankSummaryAmount(text, SummaryAmountType.NewBalance)
            ?? 0m;
        var minimumPayment = headerAmounts.MinimumPayment
            ?? ExtractNationalBankSummaryAmount(text, SummaryAmountType.MinimumPayment)
            ?? 0m;
        var pointsEarned = ExtractNationalBankPointsEarned(text);

        var analysis = BuildAnalysis(
            fileName,
            "National Bank Mastercard",
            "National Bank",
            "CreditCard",
            transactions,
            interestPaid,
            0,
            newBalance,
            minimumPayment,
            0,
            ExtractNationalBankDueDate(text),
            pointsEarned,
            ExtractInt(text, @"PREVIOUS\s*POINTS\s*BALANCE\s*:\s*(?<value>[\d,]+)"),
            ExtractInt(text, @"NEW\s*POINTS\s*BALANCE\s*:\s*(?<value>[\d,]+)"),
            null,
            null,
            null,
            ["National Bank points are treated as an estimated value using 1 point = $0.01 for card recommendations. Adjust this later if your redemption value is different."]);

        var rates = ExtractNationalBankInterestRates(text);
        analysis.PurchaseInterestRate = rates.PurchaseRate;
        analysis.CashAdvanceInterestRate = rates.CashAdvanceRate;
        return analysis;
    }

    private static StatementAnalysisResponse AnalyzeScotiaMomentum(string fileName, string text, StatementType statementType)
    {
        var statementYear = ExtractInt(text, @"Statement\s*Date\s+[A-Za-z]{3}\s+\d{1,2},\s+(?<value>\d{4})") ?? DateTime.Today.Year;
        var transactions = ParseScotiaTransactionsFromLines(text, statementType, statementYear);

        var interestPaid = ExtractMoney(text, @"Total\s*Interest\s*Charged\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})")
            ?? ExtractMoney(text, @"Interest\s*\+\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})")
            ?? 0m;
        var newBalance = ExtractMoney(text, @"New\s*Balance[^=\d]*=\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})")
            ?? ExtractMoney(text, @"Account\s*Balance[^=\d]*=\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})")
            ?? 0m;
        var minimumPayment = ExtractMoney(text, @"Total\s*Minimum\s*Payment\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})")
            ?? ExtractMoney(text, @"Current\s*minimum\s*payment\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})")
            ?? 0m;

        var analysis = BuildAnalysis(
            fileName,
            "Scotia Momentum Visa Infinite",
            "Scotiabank",
            "CreditCard",
            transactions,
            interestPaid,
            ExtractMoney(text, @"BALANCE\s*TRANSFER\s*FEE\s+(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})") ?? 0m,
            newBalance,
            minimumPayment,
            0,
            ExtractString(text, @"Payment\s*Due\s*Date\s+(?<value>[A-Za-z]{3}\s+\d{1,2},\s+\d{4})"),
            null,
            null,
            null,
            ExtractMoney(text, @"Earned\s*this\s*statement\s*period\s*\+\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})"),
            ExtractMoney(text, @"Redeemed\s*this\s*statement\s*period\s*-\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})"),
            ExtractMoney(text, @"Total\s*Cash\s*Back\s*Balance\s*=\s*\$?\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})"),
            []);

        var rates = ExtractScotiaInterestRates(text);
        analysis.PurchaseInterestRate = rates.PurchaseRate;
        analysis.CashAdvanceInterestRate = rates.CashAdvanceRate;
        return analysis;
    }


    private static List<ImportPreviewTransaction> ParseNationalBankTransactionsFromLines(
        string text,
        StatementType statementType,
        int statementYear,
        int statementMonth)
    {
        var lines = GetCleanLines(text);
        var transactions = new List<ImportPreviewTransaction>();

        for (var index = 0; index < lines.Count - 5; index++)
        {
            var transDateMatch = Regex.Match(lines[index], @"^(?<month>\d{2})\s+(?<day>\d{2})$");
            if (!transDateMatch.Success || !Regex.IsMatch(lines[index + 1], @"^[A-Z0-9]{8,12}$"))
            {
                continue;
            }

            if (!Regex.IsMatch(lines[index + 2], @"^\d{2}$") || !Regex.IsMatch(lines[index + 3], @"^\d{2}$"))
            {
                continue;
            }

            var amountIndex = -1;
            for (var searchIndex = index + 4; searchIndex < Math.Min(lines.Count, index + 22); searchIndex++)
            {
                if (IsMoneyLine(lines[searchIndex]))
                {
                    amountIndex = searchIndex;
                    break;
                }
            }

            if (amountIndex < 0)
            {
                continue;
            }

            var description = CleanDescription(string.Join(" ", lines.Skip(index + 4).Take(amountIndex - index - 4)));
            if (string.IsNullOrWhiteSpace(description) ||
                description.Contains("ORIGINAL AMOUNT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (description.Contains("FINANCE", StringComparison.OrdinalIgnoreCase) &&
                description.Contains("CHARGE", StringComparison.OrdinalIgnoreCase))
            {
                index = amountIndex;
                continue;
            }

            var month = int.Parse(transDateMatch.Groups["month"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(transDateMatch.Groups["day"].Value, CultureInfo.InvariantCulture);
            var year = month > statementMonth ? statementYear - 1 : statementYear;
            var amountText = lines[amountIndex];
            var amount = ParseMoney(amountText);
            var isCredit = amountText.EndsWith("-", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("PAYMENT RECEIVED", StringComparison.OrdinalIgnoreCase);
            var sourceCategory = GuessSourceCategory(description);

            transactions.Add(new ImportPreviewTransaction
            {
                TransactionDate = new DateOnly(year, month, day),
                Description = ToTitle(description),
                SourceCategory = sourceCategory,
                RawAmount = amount,
                TransactionGroup = statementType == StatementType.CreditCard
                    ? isCredit ? "PaymentOrRefund" : "Expense"
                    : "Unclassified",
                AppCategory = StatementCategorizer.CategorizeTransaction(description, sourceCategory)
            });

            index = amountIndex;
        }

        if (transactions.Count == 0)
        {
            return ParseNationalBankTransactionsFromNormalizedText(text, statementType, statementYear, statementMonth);
        }

        return transactions;
    }

    private static List<ImportPreviewTransaction> ParseScotiaTransactionsFromLines(
        string text,
        StatementType statementType,
        int statementYear)
    {
        var lines = GetCleanLines(text);
        var transactions = new List<ImportPreviewTransaction>();

        for (var index = 0; index < lines.Count - 4; index++)
        {
            if (!Regex.IsMatch(lines[index], @"^\d{3}$") ||
                !Regex.IsMatch(lines[index + 1], @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2}$", RegexOptions.IgnoreCase) ||
                !Regex.IsMatch(lines[index + 2], @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2}$", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var amountIndex = -1;
            for (var searchIndex = index + 3; searchIndex < Math.Min(lines.Count, index + 18); searchIndex++)
            {
                if (IsMoneyLine(lines[searchIndex]))
                {
                    amountIndex = searchIndex;
                    break;
                }
            }

            if (amountIndex < 0)
            {
                continue;
            }

            var transDateParts = lines[index + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var month = MonthNameToNumber(transDateParts[0]);
            var day = int.Parse(transDateParts[1], CultureInfo.InvariantCulture);
            var description = CleanDescription(string.Join(" ", lines.Skip(index + 3).Take(amountIndex - index - 3)));
            var amount = ParseMoney(lines[amountIndex]);
            var isCredit = (amountIndex + 1 < lines.Count && lines[amountIndex + 1] == "-") ||
                description.Contains("PAYMENT FROM", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("CASH BACK REDEEMED", StringComparison.OrdinalIgnoreCase);
            var sourceCategory = GuessSourceCategory(description);

            transactions.Add(new ImportPreviewTransaction
            {
                TransactionDate = new DateOnly(statementYear, month, day),
                Description = ToTitle(description),
                SourceCategory = sourceCategory,
                RawAmount = amount,
                TransactionGroup = statementType == StatementType.CreditCard
                    ? isCredit ? "PaymentOrRefund" : "Expense"
                    : "Unclassified",
                AppCategory = StatementCategorizer.CategorizeTransaction(description, sourceCategory)
            });

            index = amountIndex;
        }

        if (transactions.Count == 0)
        {
            return ParseScotiaTransactionsFromNormalizedText(text, statementType, statementYear);
        }

        return transactions;
    }


    private static List<ImportPreviewTransaction> ParseNationalBankTransactionsFromNormalizedText(
        string text,
        StatementType statementType,
        int statementYear,
        int statementMonth)
    {
        var normalizedText = CleanDescription(text);
        var transactions = new List<ImportPreviewTransaction>();
        var pattern = new Regex(
            @"(?<month>\d{2})\s+(?<day>\d{2})\s+[A-Z0-9]{8,12}\s+\d{2}\s+\d{2}\s+(?<description>.*?)(?<amount>\d{1,3}(?:,\d{3})*\.\d{2}-?)(?=\s+\d{2}\s+\d{2}\s+[A-Z0-9]{8,12}\s+\d{2}\s+\d{2}|\s+A LA CARTE|\s+ORIGINAL AMOUNT|\s+TRANSACTIONS\s+|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in pattern.Matches(normalizedText))
        {
            var description = CleanDescription(match.Groups["description"].Value);
            if (string.IsNullOrWhiteSpace(description) ||
                description.Contains("ORIGINAL AMOUNT", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("FINANCE CHARGE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
            if (month is < 1 or > 12 || day is < 1 or > 31)
            {
                continue;
            }

            var year = month > statementMonth ? statementYear - 1 : statementYear;
            var amountText = match.Groups["amount"].Value;
            var amount = ParseMoney(amountText);
            if (amount <= 0) continue;

            var isCredit = amountText.EndsWith("-", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("PAYMENT RECEIVED", StringComparison.OrdinalIgnoreCase);
            var sourceCategory = GuessSourceCategory(description);

            transactions.Add(new ImportPreviewTransaction
            {
                TransactionDate = new DateOnly(year, month, day),
                Description = ToTitle(description),
                SourceCategory = sourceCategory,
                RawAmount = amount,
                TransactionGroup = statementType == StatementType.CreditCard
                    ? isCredit ? "PaymentOrRefund" : "Expense"
                    : "Unclassified",
                AppCategory = StatementCategorizer.CategorizeTransaction(description, sourceCategory)
            });
        }

        return transactions;
    }

    private static List<ImportPreviewTransaction> ParseScotiaTransactionsFromNormalizedText(
        string text,
        StatementType statementType,
        int statementYear)
    {
        var normalizedText = CleanDescription(text);
        var transactions = new List<ImportPreviewTransaction>();
        var pattern = new Regex(
            @"(?<ref>\d{3})\s+(?<transMonth>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+(?<transDay>\d{1,2})\s+(?<postMonth>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+(?<postDay>\d{1,2})\s+(?<description>.*?)(?<amount>\d{1,3}(?:,\d{3})*\.\d{2}-?)(?=\s+\d{3}\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)|\s+SUB-TOTAL|\s+Interest charges|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in pattern.Matches(normalizedText))
        {
            var description = CleanDescription(match.Groups["description"].Value);
            if (string.IsNullOrWhiteSpace(description)) continue;

            var month = MonthNameToNumber(match.Groups["transMonth"].Value);
            var day = int.Parse(match.Groups["transDay"].Value, CultureInfo.InvariantCulture);
            var amountText = match.Groups["amount"].Value;
            var amount = ParseMoney(amountText);
            if (amount <= 0) continue;

            var isCredit = amountText.EndsWith("-", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("PAYMENT FROM", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("CASH BACK REDEEMED", StringComparison.OrdinalIgnoreCase);
            var sourceCategory = GuessSourceCategory(description);

            transactions.Add(new ImportPreviewTransaction
            {
                TransactionDate = new DateOnly(statementYear, month, day),
                Description = ToTitle(description),
                SourceCategory = sourceCategory,
                RawAmount = amount,
                TransactionGroup = statementType == StatementType.CreditCard
                    ? isCredit ? "PaymentOrRefund" : "Expense"
                    : "Unclassified",
                AppCategory = StatementCategorizer.CategorizeTransaction(description, sourceCategory)
            });
        }

        return transactions;
    }

    private static async Task<StatementAnalysisResponse> AnalyzeCsvAsync(IFormFile file, StatementType statementType)
    {
        var transactions = new List<ImportPreviewTransaction>();
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var headerLine = await reader.ReadLineAsync();

        if (headerLine is null)
        {
            return BuildAnalysis(file.FileName, Path.GetFileNameWithoutExtension(file.FileName), "CSV", statementType.ToString(), [], 0, 0, 0, 0, 0, null, null, null, null, null, null, null, ["CSV file is empty."]);
        }

        var delimiter = headerLine.Contains(';') ? ';' : ',';
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var columns = line.Split(delimiter);
            if (columns.Length < 6) continue;
            if (!DateOnly.TryParse(columns[0].Trim().Trim('"'), out var transactionDate)) continue;

            var description = columns[2].Trim().Trim('"');
            var sourceCategory = columns[3].Trim().Trim('"');
            var debitAmount = ParseMoney(columns[4].Trim().Trim('"'));
            var creditAmount = ParseMoney(columns[5].Trim().Trim('"'));
            var rawAmount = debitAmount > 0 ? debitAmount : creditAmount;
            if (rawAmount <= 0) continue;
            var isCredit = creditAmount > 0;

            transactions.Add(new ImportPreviewTransaction
            {
                TransactionDate = transactionDate,
                Description = description,
                SourceCategory = sourceCategory,
                RawAmount = rawAmount,
                TransactionGroup = statementType == StatementType.CreditCard ? isCredit ? "PaymentOrRefund" : "Expense" : "Unclassified",
                AppCategory = StatementCategorizer.CategorizeTransaction(description, sourceCategory)
            });
        }

        return BuildAnalysis(file.FileName, Path.GetFileNameWithoutExtension(file.FileName), "CSV", statementType.ToString(), transactions, 0, 0, 0, 0, 0, null, null, null, null, null, null, null, []);
    }

    private static StatementAnalysisResponse BuildAnalysis(
        string fileName,
        string accountName,
        string institution,
        string statementKind,
        List<ImportPreviewTransaction> transactions,
        decimal interestPaid,
        decimal feesPaid,
        decimal newBalance,
        decimal minimumPaymentDue,
        decimal unused,
        string? dueDate,
        int? pointsEarned,
        int? previousPointsBalance,
        int? newPointsBalance,
        decimal? cashBackEarned,
        decimal? cashBackRedeemed,
        decimal? cashBackBalance,
        List<string> notes)
    {
        var totalExpenses = transactions.Where(transaction => transaction.TransactionGroup == "Expense").Sum(transaction => transaction.RawAmount);
        var totalPayments = transactions.Where(transaction => transaction.TransactionGroup == "PaymentOrRefund").Sum(transaction => transaction.RawAmount);
        var categoryTotals = transactions
            .Where(transaction => transaction.TransactionGroup == "Expense")
            .GroupBy(transaction => transaction.AppCategory)
            .Select(group => new ImportCategoryTotal
            {
                Category = group.Key,
                Total = group.Sum(transaction => transaction.RawAmount)
            })
            .OrderByDescending(category => category.Total)
            .ToList();

        var estimatedRewardValue = (pointsEarned ?? 0) * AssumedNationalBankPointValue + (cashBackEarned ?? 0);
        var effectiveRewardRate = totalExpenses > 0 ? estimatedRewardValue / totalExpenses : 0;
        var rewardSummary = pointsEarned is not null
            ? $"Earned {pointsEarned:N0} points. Estimated statement reward value is {estimatedRewardValue:C} using 1 point = {AssumedNationalBankPointValue:C}."
            : cashBackEarned is not null
                ? $"Earned {cashBackEarned:C} cash back this statement. Redeemed {(cashBackRedeemed ?? 0):C}. Current cash back balance is {(cashBackBalance ?? 0):C}."
                : "No points or cash back details were detected in this statement.";

        return new StatementAnalysisResponse
        {
            FileName = fileName,
            AccountName = accountName,
            Institution = institution,
            ProductName = accountName,
            StatementKind = statementKind,
            TotalExpenses = totalExpenses,
            TotalPaymentsOrRefunds = totalPayments,
            NetActivity = totalPayments - totalExpenses,
            InterestPaid = interestPaid,
            FeesPaid = feesPaid,
            NewBalance = newBalance,
            MinimumPaymentDue = minimumPaymentDue,
            PaymentDueDate = dueDate,
            PointsEarned = pointsEarned,
            PreviousPointsBalance = previousPointsBalance,
            NewPointsBalance = newPointsBalance,
            CashBackEarned = cashBackEarned,
            CashBackRedeemed = cashBackRedeemed,
            CashBackBalance = cashBackBalance,
            EstimatedRewardValue = estimatedRewardValue,
            EffectiveRewardRate = effectiveRewardRate,
            RewardSummary = rewardSummary,
            CategoryTotals = categoryTotals,
            Transactions = transactions,
            Notes = notes
        };
    }

    private static List<CategoryExpenseAdviceResponse> BuildCategoryExpenseAdvice(
        List<StatementAnalysisResponse> statements,
        List<string> categories)
    {
        var advice = new List<CategoryExpenseAdviceResponse>();

        foreach (var category in categories)
        {
            var spendingByStatement = statements
                .Select(statement => new
                {
                    Statement = statement,
                    Expense = statement.CategoryTotals
                        .Where(total => total.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                        .Sum(total => total.Total),
                    Rate = EstimateRewardRate(statement, category)
                })
                .Where(item => item.Expense > 0)
                .OrderByDescending(item => item.Expense)
                .ToList();

            if (spendingByStatement.Count == 0)
            {
                continue;
            }

            var allCandidates = statements
                .Select(statement => new
                {
                    Statement = statement,
                    Rate = EstimateRewardRate(statement, category)
                })
                .OrderByDescending(item => item.Rate)
                .ThenBy(item => item.Statement.InterestPaid)
                .ToList();

            var winner = allCandidates.First();
            var totalExpense = spendingByStatement.Sum(item => item.Expense);
            var currentEstimatedRewardValue = spendingByStatement.Sum(item => item.Expense * item.Rate);
            var winnerEstimatedRewardValue = totalExpense * winner.Rate;
            var cardUsed = string.Join(", ", spendingByStatement.Select(item => $"{item.Statement.AccountName} ({item.Expense:C})"));
            var rewardLabel = string.Join(", ", spendingByStatement.Select(item =>
            {
                var rewardType = GetRewardType(item.Statement);
                var estimatedValue = item.Expense * item.Rate;
                return $"{item.Statement.AccountName}: {estimatedValue:C} estimated {rewardType}";
            }));

            var reason = winnerEstimatedRewardValue > currentEstimatedRewardValue + 0.01m
                ? $"Move this category to {winner.Statement.AccountName}. Estimated reward value improves from {currentEstimatedRewardValue:C} to {winnerEstimatedRewardValue:C} for this statement spend."
                : $"Current card choice is already close to the best detected option. {winner.Statement.AccountName} has the highest estimated reward rate at {(winner.Rate * 100):0.##}%.";

            advice.Add(new CategoryExpenseAdviceResponse
            {
                Category = category,
                Expense = totalExpense,
                EstimatedPointsOrCashBackValue = currentEstimatedRewardValue,
                PointsOrCashBack = rewardLabel,
                CardUsed = cardUsed,
                AdvisedCardToUse = winner.Statement.AccountName,
                Reason = reason
            });
        }

        return advice
            .OrderByDescending(item => item.Expense)
            .ToList();
    }

    private static void ApplyPaymentPriorities(List<StatementAnalysisResponse> statements)
    {
        var ordered = statements
            .OrderByDescending(statement => statement.InterestPaid > 0)
            .ThenByDescending(statement => statement.InterestPaid)
            .ThenByDescending(statement => statement.NewBalance)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var statement = ordered[index];
            statement.PaymentPriority = index == 0 ? "Pay First" : index == 1 ? "Pay Next" : "Can Pay Later";
            statement.PaymentPriorityReason = statement.InterestPaid > 0
                ? $"Interest was charged on this statement ({statement.InterestPaid:C}). Prioritize interest-bearing balances first."
                : statement.NewBalance > 0
                    ? "No interest was detected, but the statement has an outstanding balance. Pay at least the minimum before the due date."
                    : "No outstanding balance was detected from the statement summary.";
        }
    }

    private static decimal EstimateRewardRate(StatementAnalysisResponse statement, string category)
    {
        if (statement.Institution.Contains("Scotia", StringComparison.OrdinalIgnoreCase))
        {
            return category switch
            {
                "Groceries" => 0.04m,
                "Phone / Internet" => 0.04m,
                "Transportation" => 0.02m,
                "Restaurants / Coffee" => 0.01m,
                "Entertainment" => 0.01m,
                "Software / Subscriptions" => 0.04m,
                _ => 0.01m
            };
        }

        if (statement.Institution.Contains("National", StringComparison.OrdinalIgnoreCase))
        {
            return category switch
            {
                "Groceries" => 2m * AssumedNationalBankPointValue,
                "Restaurants / Coffee" => 2m * AssumedNationalBankPointValue,
                "Phone / Internet" => 1.5m * AssumedNationalBankPointValue,
                "Software / Subscriptions" => 1.5m * AssumedNationalBankPointValue,
                _ => 1m * AssumedNationalBankPointValue
            };
        }

        if (statement.CashBackEarned is > 0 && statement.TotalExpenses > 0)
        {
            return statement.CashBackEarned.Value / statement.TotalExpenses;
        }

        return 0m;
    }

    private static string GetRewardType(StatementAnalysisResponse statement)
    {
        if (statement.CashBackEarned is not null || statement.CashBackBalance is not null) return "cash back";
        if (statement.PointsEarned is not null || statement.NewPointsBalance is not null) return "points";
        return "estimated rewards";
    }

    private static string GuessSourceCategory(string description)
    {
        var value = description.ToLowerInvariant();
        if (value.Contains("payment") || value.Contains("cash back redeemed")) return "Credit card payment";
        if (value.Contains("finance charge") || value.Contains("interest")) return "Fees / Interest";
        if (value.Contains("costco") || value.Contains("walmart") || value.Contains("wal-mart") || value.Contains("supermarket") || value.Contains("metro") || value.Contains("rcss")) return "Grocery";
        if (value.Contains("restaurant") || value.Contains("tim hortons") || value.Contains("ubereats") || value.Contains("cuisine") || value.Contains("sweets")) return "Restaurants";
        if (value.Contains("esso") || value.Contains("uber") || value.Contains("parking") || value.Contains("chargepoint") || value.Contains("drivetest") || value.Contains("bridge")) return "Car rentals & taxis";
        if (value.Contains("bell") || value.Contains("netflix") || value.Contains("apple.com") || value.Contains("applecare") || value.Contains("claude") || value.Contains("openai")) return "Internet";
        if (value.Contains("lcbo") || value.Contains("beer") || value.Contains("cinema") || value.Contains("cineplex")) return "Alcohol/bars";
        if (value.Contains("insurance")) return "Insurance / Financial";
        if (value.Contains("temple") || value.Contains("donation")) return "Gifts and donations";
        return "Shopping";
    }


    private static List<string> GetCleanLines(string text) => text
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => CleanDescription(line))
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToList();

    private static bool IsMoneyLine(string value) => Regex.IsMatch(
        value.Trim(),
        @"^\$?\d{1,3}(?:,\d{3})*\.\d{2}-?$|^\$?\d+\.\d{2}-?$");

    private static int ExtractNationalBankStatementYear(string text) => 2000 + (ExtractInt(text, @"(?<value>\d{2})\s+\d{2}\s+\d{2}\s+\$\d") ?? DateTime.Today.Year % 100);
    private static int ExtractNationalBankStatementMonth(string text) => ExtractInt(text, @"\d{2}\s+(?<value>\d{2})\s+\d{2}\s+\$\d") ?? DateTime.Today.Month;
    private static string? ExtractNationalBankDueDate(string text) => ExtractString(text, @"(?<value>20\d{2}\s+\d{2}\s+\d{2})");

    private static int? ExtractNationalBankPointsEarned(string text)
    {
        var grocery = ExtractInt(text, @"GROCERY STORES/RESTAURANTS\s*:\s*(?<value>[\d,]+)") ?? 0;
        var gas = ExtractInt(text, @"GAS/ELECTRIC CHARGE\s*:\s*(?<value>[\d,]+)") ?? 0;
        var recurring = ExtractInt(text, @"RECURRING BILLS\s*:\s*(?<value>[\d,]+)") ?? 0;
        var other = ExtractInt(text, @"OTHER PURCHASES\s*:\s*(?<value>[\d,]+)") ?? 0;
        var total = grocery + gas + recurring + other;
        return total > 0 ? total : null;
    }


    private static decimal? ExtractNationalBankFinanceCharge(string text)
    {
        // PdfPig extraction can insert or remove spaces around the finance charge amount.
        // Support examples like "FINANCE CHARGE 208.52" and "FINANCE CHARGE208.52".
        return ExtractMoney(text, @"FINANCE\s*CHARGE\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})");
    }

    private static (decimal? NewBalance, decimal? MinimumPayment) ExtractNationalBankHeaderAmounts(string text)
    {
        // National Bank header commonly appears as:
        // 26 06 09 $10,991.54 $275.00 2026 06 30
        // Some PDF extraction layouts remove spaces, so make spacing and dollar signs optional.
        var match = Regex.Match(
            text,
            @"\d{2}\s*\d{2}\s*\d{2}\s*\$?\s*(?<newBalance>\d{1,3}(?:,\d{3})*\.\d{2})\s*\$?\s*(?<minimum>\d{1,3}(?:,\d{3})*\.\d{2})\s*20\d{2}\s*\d{2}\s*\d{2}",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
        {
            return (null, null);
        }

        return (
            ParseMoney(match.Groups["newBalance"].Value),
            ParseMoney(match.Groups["minimum"].Value));
    }

    private enum SummaryAmountType
    {
        NewBalance,
        MinimumPayment,
        Interest
    }

    private static decimal? ExtractNationalBankSummaryAmount(string text, SummaryAmountType amountType)
    {
        // National Bank summary line commonly appears as:
        // previous balance, purchases, interest, fees, payments/credits, new balance.
        var summaryPattern = new Regex(
            @"(?<previous>\d{1,3}(?:,\d{3})*\.\d{2})\s+(?<purchases>\d{1,3}(?:,\d{3})*\.\d{2})\s+(?<interest>\d{1,3}(?:,\d{3})*\.\d{2})\s+(?<fees>\d{1,3}(?:,\d{3})*\.\d{2})\s+(?<payments>\d{1,3}(?:,\d{3})*\.\d{2})\s+(?<newBalance>\d{1,3}(?:,\d{3})*\.\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var matches = summaryPattern.Matches(text);
        if (matches.Count > 0)
        {
            var match = matches[^1];
            return amountType switch
            {
                SummaryAmountType.Interest => ParseMoney(match.Groups["interest"].Value),
                SummaryAmountType.NewBalance => ParseMoney(match.Groups["newBalance"].Value),
                _ => null
            };
        }

        // Header line commonly appears as: statement date, new balance, minimum payment, due date.
        var dueLine = Regex.Match(
            text,
            @"\d{2}\s+\d{2}\s+\d{2}\s+\$(?<newBalance>\d{1,3}(?:,\d{3})*\.\d{2})\s+\$(?<minimum>\d{1,3}(?:,\d{3})*\.\d{2})\s+20\d{2}",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (dueLine.Success)
        {
            return amountType switch
            {
                SummaryAmountType.NewBalance => ParseMoney(dueLine.Groups["newBalance"].Value),
                SummaryAmountType.MinimumPayment => ParseMoney(dueLine.Groups["minimum"].Value),
                _ => null
            };
        }

        return null;
    }

    private static (decimal? PurchaseRate, decimal? CashAdvanceRate) ExtractNationalBankInterestRates(string text)
    {
        var match = Regex.Match(text, @"(?<purchase>\d{1,2}\.\d{2})\s*%\s+(?<cash>\d{1,2}\.\d{2})\s*%", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success
            ? (ParseMoney(match.Groups["purchase"].Value), ParseMoney(match.Groups["cash"].Value))
            : (null, null);
    }

    private static (decimal? PurchaseRate, decimal? CashAdvanceRate) ExtractScotiaInterestRates(string text)
    {
        var purchase = ExtractMoney(text, @"Purchases\s*(?<amount>\d{1,2}\.\d{2})\s*%");
        var cashAdvance = ExtractMoney(text, @"Cash\s*Advances\s*(?<amount>\d{1,2}\.\d{2})\s*%");
        return (purchase, cashAdvance);
    }

    private static decimal? ExtractMoney(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? ParseMoney(match.Groups["amount"].Value) : null;
    }

    private static int? ExtractInt(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return null;
        return int.TryParse(match.Groups["value"].Value.Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string? ExtractString(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static decimal ParseMoney(string value)
    {
        var clean = value.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;
    }

    private static int MonthNameToNumber(string value) => DateTime.ParseExact(value, "MMM", CultureInfo.InvariantCulture).Month;

    private static string CleanDescription(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static string ToTitle(string value) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
}
