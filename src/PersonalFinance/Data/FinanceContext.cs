using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Models;

namespace PersonalFinance.Data;

// Контекст Entity Framework Core для базы данных PersonalFinanceDB
public class FinanceContext : DbContext
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();
    public DbSet<BudgetExecution> BudgetExecutions => Set<BudgetExecution>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlServer(Db.ConnectionString);

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        => builder.Properties<decimal>().HavePrecision(18, 2);

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Представление без первичного ключа
        model.Entity<BudgetExecution>().HasNoKey().ToView("vw_BudgetExecution");

        // На этих таблицах есть триггеры: EF Core должен знать о них,
        // чтобы не использовать несовместимую с триггерами конструкцию OUTPUT
        model.Entity<Transaction>().ToTable(t =>
        {
            t.HasTrigger("trg_transactions_balance");
            t.HasTrigger("trg_transactions_budget_notify");
        });
        model.Entity<Category>().ToTable(t => t.HasTrigger("trg_categories_type_lock"));
        model.Entity<Budget>().ToTable(t => t.HasTrigger("trg_budgets_expense_only"));
        model.Entity<RecurringPayment>().ToTable(t => t.HasTrigger("trg_recurring_owner"));

        foreach (var entity in model.Model.GetEntityTypes())
        {
            // Имена таблиц и столбцов в БД записаны в стиле snake_case:
            // RecurringPayments -> recurring_payments, FullName -> full_name
            if (entity.GetTableName() is string table)
                entity.SetTableName(ToSnakeCase(table));
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));

            // Удаление связанных записей запрещено, как и в самой БД (NO ACTION)
            foreach (var fk in entity.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    // Создание пользователя вместе со стандартным набором категорий (в одной транзакции)
    public User CreateUser(string fullName, string email, string password, string roleName)
    {
        using var transaction = Database.BeginTransaction();
        var user = new User
        {
            RoleId = Roles.Single(r => r.Name == roleName).Id,
            FullName = fullName,
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            CreatedAt = DateTime.Now
        };
        Users.Add(user);
        SaveChanges();
        Database.ExecuteSql($"EXEC sp_CreateDefaultCategories @UserId = {user.Id}");
        transaction.Commit();
        return user;
    }

    private static string ToSnakeCase(string name)
        => Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
}
