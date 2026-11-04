using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinance.Models;

// Классы-сущности соответствуют таблицам базы данных PersonalFinanceDB.
// Свойства только для чтения (без set) не сохраняются в БД и служат для отображения.

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class User
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public Role? Role { get; set; }
}

public class Currency
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public string Display => $"{Code} — {Name}";
}

public class Account
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public string Name { get; set; } = "";
    public decimal Balance { get; set; }

    public Currency? Currency { get; set; }

    public string Display => Currency == null ? Name : $"{Name} ({Currency.Code})";
}

public class Category
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Icon { get; set; }

    public string Display => $"{Icon} {Name} ({Type})".Trim();
}

public class Transaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    [Column(TypeName = "date")]
    public DateTime TransactionDate { get; set; }
    public string? Comment { get; set; }

    public Account? Account { get; set; }
    public Category? Category { get; set; }
}

public class Budget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal LimitAmount { get; set; }
    public string Period { get; set; } = "";
    [Column(TypeName = "date")]
    public DateTime StartDate { get; set; }
    [Column(TypeName = "date")]
    public DateTime EndDate { get; set; }

    public Category? Category { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? BudgetId { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Goal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? AccountId { get; set; }
    public string Name { get; set; } = "";
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    [Column(TypeName = "date")]
    public DateTime? Deadline { get; set; }

    public Account? Account { get; set; }

    public double Progress =>
        TargetAmount > 0 ? (double)Math.Min(100m, CurrentAmount * 100m / TargetAmount) : 0;
}

public class RecurringPayment
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Frequency { get; set; } = "";
    [Column(TypeName = "date")]
    public DateTime NextPaymentDate { get; set; }

    public Account? Account { get; set; }
    public Category? Category { get; set; }
}

// Строка представления vw_BudgetExecution (только чтение)
public class BudgetExecution
{
    public int BudgetId { get; set; }
    public int UserId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Period { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentUsed { get; set; }

    public bool IsExceeded => Spent > LimitAmount;
}
