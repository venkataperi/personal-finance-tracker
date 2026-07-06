using FinanceTracker.Api.Modules.Finance.Categories;
using FinanceTracker.Api.Modules.Finance.Transactions;
using FinanceTracker.Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Api.Modules.Imports;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "Finance Tracker API is running",
        timestamp = DateTime.UtcNow
    });
})
.WithName("HealthCheck");

app.MapPost("/api/categories", async (
    CreateCategoryRequest request,
    AppDbContext dbContext) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest("Category name is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Type))
    {
        return Results.BadRequest("Category type is required.");
    }

    var category = new Category
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim(),
        Type = request.Type.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };

    dbContext.Categories.Add(category);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/categories/{category.Id}", category);
})
.WithName("CreateCategory");

app.MapGet("/api/categories", async (AppDbContext dbContext) =>
{
    var categories = await dbContext.Categories
        .OrderBy(category => category.Name)
        .ToListAsync();

    return Results.Ok(categories);
})
.WithName("GetCategories");

app.MapPost("/api/transactions", async (
    CreateTransactionRequest request,
    AppDbContext dbContext) =>
{
    if (request.CategoryId == Guid.Empty)
    {
        return Results.BadRequest("CategoryId is required.");
    }

    if (request.Amount <= 0)
    {
        return Results.BadRequest("Amount must be greater than zero.");
    }

    if (string.IsNullOrWhiteSpace(request.Type))
    {
        return Results.BadRequest("Transaction type is required.");
    }

    var categoryExists = await dbContext.Categories
        .AnyAsync(category => category.Id == request.CategoryId);

    if (!categoryExists)
    {
        return Results.BadRequest("Category does not exist.");
    }

    var transaction = new Transaction
    {
        Id = Guid.NewGuid(),
        CategoryId = request.CategoryId,
        Amount = request.Amount,
        Type = request.Type.Trim(),
        TransactionDate = request.TransactionDate,
        Notes = request.Notes,
        CreatedAtUtc = DateTime.UtcNow
    };

    dbContext.Transactions.Add(transaction);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/transactions/{transaction.Id}", transaction);
})
.WithName("CreateTransaction");

app.MapGet("/api/transactions", async (AppDbContext dbContext) =>
{
    var transactions = await dbContext.Transactions
        .Join(
            dbContext.Categories,
            transaction => transaction.CategoryId,
            category => category.Id,
            (transaction, category) => new
            {
                transaction.Id,
                transaction.CategoryId,
                CategoryName = category.Name,
                transaction.Amount,
                transaction.Type,
                transaction.TransactionDate,
                transaction.Notes,
                transaction.CreatedAtUtc
            })
        .OrderByDescending(transaction => transaction.TransactionDate)
        .ToListAsync();

    return Results.Ok(transactions);
})
.WithName("GetTransactions");

app.MapGet("/api/reports/summary", async (AppDbContext dbContext) =>
{
    var totalIncome = await dbContext.Transactions
        .Where(transaction => transaction.Type == "Income")
        .Select(transaction => (decimal?)transaction.Amount)
        .SumAsync() ?? 0;

    var totalExpenses = await dbContext.Transactions
        .Where(transaction => transaction.Type == "Expense")
        .Select(transaction => (decimal?)transaction.Amount)
        .SumAsync() ?? 0;

    var transactionCount = await dbContext.Transactions.CountAsync();

    var balance = totalIncome - totalExpenses;

    return Results.Ok(new
    {
        totalIncome,
        totalExpenses,
        balance,
        transactionCount
    });
})
.WithName("GetSummaryReport");

app.MapPost("/api/imports/statement/preview", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart form data.");
    }

    var form = await request.ReadFormAsync();
    var statementType = form["statementType"].ToString();
    var file = form.Files["file"];


    if (string.IsNullOrWhiteSpace(statementType))
    {
        return Results.BadRequest("Statement type is required.");
    }

    if (!Enum.TryParse<StatementType>(statementType, true, out var parsedStatementType))
    {
        return Results.BadRequest("Invalid statement type.");
    }

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("CSV file is required.");
    }

    var previewTransactions = new List<ImportPreviewTransaction>();

    using var stream = file.OpenReadStream();
    using var reader = new StreamReader(stream);

    var headerLine = await reader.ReadLineAsync();

    if (headerLine is null)
    {
        return Results.BadRequest("CSV file is empty.");
    }

     var delimiter = headerLine.Contains(';') ? ';' : ',';

    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

    var columns = line.Split(delimiter);

if (columns.Length < 6)
{
    continue;
}

if (!DateOnly.TryParse(columns[0].Trim().Trim('"'), out var transactionDate))
{
    continue;
}

var description = columns[2].Trim().Trim('"');
var sourceCategory = columns[3].Trim().Trim('"');
var debitText = columns[4].Trim().Trim('"').Replace("$", string.Empty);
var creditText = columns[5].Trim().Trim('"').Replace("$", string.Empty);

decimal.TryParse(
    debitText,
    NumberStyles.Number,
    CultureInfo.InvariantCulture,
    out var debitAmount);

decimal.TryParse(
    creditText,
    NumberStyles.Number,
    CultureInfo.InvariantCulture,
    out var creditAmount);

var rawAmount = debitAmount > 0 ? debitAmount : creditAmount;
var isCreditAmount = creditAmount > 0;

if (rawAmount <= 0)
{
    continue;
}

var transactionGroup = parsedStatementType == StatementType.CreditCard
    ? isCreditAmount ? "PaymentOrRefund" : "Expense"
    : "Unclassified";

        //var appCategory = sourceCategory;

        var descriptionLower = description.ToLowerInvariant();
var sourceCategoryLower = sourceCategory.ToLowerInvariant();

var appCategory =
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

        previewTransactions.Add(new ImportPreviewTransaction
        {
            TransactionDate = transactionDate,
            Description = description,
            SourceCategory = sourceCategory,
            RawAmount = rawAmount,
            TransactionGroup = transactionGroup,
            AppCategory = appCategory
        });
    }

    return Results.Ok(previewTransactions);
})

.WithName("PreviewStatementImport")
.DisableAntiforgery()
.ExcludeFromDescription();

app.Run();