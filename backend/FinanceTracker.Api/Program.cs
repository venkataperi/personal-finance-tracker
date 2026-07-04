using FinanceTracker.Api.Modules.Finance.Categories;
using FinanceTracker.Api.Modules.Finance.Transactions;
using FinanceTracker.Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

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

app.Run();