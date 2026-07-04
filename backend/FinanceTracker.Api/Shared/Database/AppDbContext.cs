using FinanceTracker.Api.Modules.Finance.Budgets;
using FinanceTracker.Api.Modules.Finance.Categories;
using FinanceTracker.Api.Modules.Finance.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Shared.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Budget> Budgets => Set<Budget>();
}