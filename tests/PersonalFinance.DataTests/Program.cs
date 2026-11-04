// Проверка слоя данных приложения на реальной базе PersonalFinanceDB.
// Запускать на только что созданной базе (после PersonalFinanceDB.sql): dotnet run
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

int fails = 0;
void Check(string name, bool ok, string? extra = "") { Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {name} {extra}"); if (!ok) fails++; }
string Err(Action a) { try { a(); return "no error"; } catch (Exception ex) { return Db.ErrorMessage(ex); } }

using (var db = new FinanceContext())
{
    // все DbSet читаются (проверка сопоставления snake_case)
    Check("roles", db.Roles.Count() == 2);
    Check("users", db.Users.Count() == 3);
    Check("currencies", db.Currencies.Count() == 4);
    Check("accounts", db.Accounts.Count() == 6);
    Check("categories", db.Categories.Count() == 36);
    Check("transactions", db.Transactions.Count() == 33);
    Check("budgets", db.Budgets.Count() == 5);
    Check("notifications", db.Notifications.Count() == 3);
    Check("goals", db.Goals.Count() == 3);
    Check("recurring", db.RecurringPayments.Count() == 4);
    var be = db.BudgetExecutions.Where(b => b.UserId == 2).ToList(); Check("view", be.Count == 4 && be.Count(b => b.IsExceeded) == 1, string.Join("; ", be.Select(b => $"{b.CategoryName} {b.Spent}/{b.LimitAmount} {b.PercentUsed}")));
}

// Пароли тестовых пользователей
using (var db = new FinanceContext())
{
    var admin = db.Users.Include(u => u.Role).Single(u => u.Email == "admin@finance.ru");
    Check("admin password", PasswordHasher.Verify("Admin123!", admin.PasswordHash) && admin.Role!.Name == Session.AdminRole);
    Check("wrong password", !PasswordHasher.Verify("admin123!", admin.PasswordHash));
    Check("ivanov password", PasswordHasher.Verify("User123!", db.Users.Single(u => u.Email == "ivanov@mail.ru").PasswordHash));
    Check("smirnova password", PasswordHasher.Verify("User123!", db.Users.Single(u => u.Email == "SMIRNOVA@mail.ru").PasswordHash), "(email без учёта регистра)");
    var h = PasswordHasher.Hash("Test1234"); Check("hash roundtrip", PasswordHasher.Verify("Test1234", h) && h.Length < 255, h);
}

// Регистрация
int newUserId;
using (var db = new FinanceContext())
{
    var u = db.CreateUser("Тестов Тест", "test@mail.ru", "Test1234", Session.UserRole);
    newUserId = u.Id;
    Check("register + categories", db.Categories.Count(c => c.UserId == u.Id) == 12);
    Check("duplicate email", Err(() => new FinanceContext().CreateUser("X Y", "TEST@mail.ru", "Test1234", Session.UserRole)).StartsWith("Такая запись"), Err(() => new FinanceContext().CreateUser("X Y", "TEST@mail.ru", "Test1234", Session.UserRole)));
    Check("register rollback", db.Users.Count(x => x.FullName == "X Y") == 0);
}

// Счета и операции через EF (с триггерами)
int accId, catExp, catInc, tId;
using (var db = new FinanceContext())
{
    var acc = new Account { UserId = newUserId, CurrencyId = 1, Name = "Карта", Balance = 1000 };
    db.Accounts.Add(acc); db.SaveChanges(); accId = acc.Id;
    catExp = db.Categories.Single(c => c.UserId == newUserId && c.Name == "Продукты").Id;
    catInc = db.Categories.Single(c => c.UserId == newUserId && c.Name == "Зарплата").Id;
    var t = db.Transactions.Add(new Transaction()).Entity;
    t.AccountId = accId; t.CategoryId = catExp; t.Amount = 250.50m; t.TransactionDate = DateTime.Today; t.Comment = "тест";
    db.SaveChanges(); tId = t.Id;
}
decimal Bal() { using var d = new FinanceContext(); return d.Accounts.Single(a => a.Id == accId).Balance; }
Check("insert expense", Bal() == 749.50m, Bal().ToString());
using (var db = new FinanceContext()) { var t = db.Transactions.Single(x => x.Id == tId); t.CategoryId = catInc; t.Amount = 100; db.SaveChanges(); }
Check("update to income", Bal() == 1100m, Bal().ToString());
using (var db = new FinanceContext()) db.Transactions.Where(x => x.Id == tId && x.Account!.UserId == newUserId).ExecuteDelete();
Check("delete", Bal() == 1000m, Bal().ToString());
using (var db = new FinanceContext()) { db.Transactions.Add(new Transaction { AccountId = accId, CategoryId = catExp, Amount = 10, TransactionDate = DateTime.Today }); db.SaveChanges(); }
Check("cross-user category", Err(() => { using var d = new FinanceContext(); d.Transactions.Add(new Transaction { AccountId = accId, CategoryId = 13, Amount = 5, TransactionDate = DateTime.Today }); d.SaveChanges(); }).Contains("разным пользователям"));
Check("delete account with ops", Err(() => { using var d = new FinanceContext(); d.Accounts.Where(a => a.Id == accId).ExecuteDelete(); }).StartsWith("Запись нельзя удалить"));
Check("type lock", Err(() => { using var d = new FinanceContext(); d.Categories.Single(c => c.Id == catExp).Type = "Доход"; d.SaveChanges(); }).StartsWith("Нельзя изменить тип"));
Check("check constraint", Err(() => { using var d = new FinanceContext(); d.Budgets.Add(new Budget { UserId = newUserId, CategoryId = catExp, LimitAmount = 5, Period = "Месяц", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(-1) }); d.SaveChanges(); }).StartsWith("Данные не прошли"));
Check("composite FK", Err(() => { using var d = new FinanceContext(); d.Budgets.Add(new Budget { UserId = newUserId, CategoryId = 13, LimitAmount = 5, Period = "Месяц", StartDate = DateTime.Today, EndDate = DateTime.Today }); d.SaveChanges(); }).StartsWith("Связанная запись"));
Check("income budget", Err(() => { using var d = new FinanceContext(); d.Budgets.Add(new Budget { UserId = newUserId, CategoryId = catInc, LimitAmount = 5, Period = "Месяц", StartDate = DateTime.Today, EndDate = DateTime.Today }); d.SaveChanges(); }).StartsWith("Бюджет можно"));

// Бюджет и уведомление
using (var db = new FinanceContext())
{
    db.Budgets.Add(new Budget { UserId = newUserId, CategoryId = catExp, LimitAmount = 100, Period = "Месяц", StartDate = DateTime.Today.AddDays(-5), EndDate = DateTime.Today.AddDays(5) });
    db.SaveChanges();
    db.Transactions.Add(new Transaction { AccountId = accId, CategoryId = catExp, Amount = 200, TransactionDate = DateTime.Today });
    db.SaveChanges();
    var n = db.Notifications.Where(x => x.UserId == newUserId).ToList();
    Check("budget notification", n.Count == 1 && n[0].BudgetId != null, n.FirstOrDefault()?.Message);
    db.Notifications.Where(x => x.UserId == newUserId && !x.IsRead).ExecuteUpdate(s => s.SetProperty(x => x.IsRead, true));
    Check("mark read", db.Notifications.Count(x => x.UserId == newUserId && !x.IsRead) == 0);
}

// Цели и регулярные платежи
using (var db = new FinanceContext())
{
    var g = db.Goals.Add(new Goal { UserId = newUserId, Name = "Цель", TargetAmount = 1000, CurrentAmount = 250 }).Entity;
    db.SaveChanges();
    Check("goal without account", db.Goals.Include(x => x.Account).Single(x => x.Id == g.Id).Account == null && g.Progress == 25);
    var list = db.Goals.Where(x => x.UserId == 2).OrderBy(x => x.Deadline == null).ThenBy(x => x.Deadline).ToList();
    Check("goals order", list.Count == 2 && list[0].Name == "Новый ноутбук");
    var r = db.RecurringPayments.Add(new RecurringPayment { AccountId = accId, CategoryId = catExp, Amount = 50, Frequency = "Еженедельно", NextPaymentDate = DateTime.Today.AddDays(-14) }).Entity;
    db.SaveChanges();
    r.Amount = 60; db.SaveChanges();
}
var processed = Db.ExecuteProcedure("sp_ProcessRecurringPayments", new SqlParameter("@UserId", newUserId));
Check("process recurring", Convert.ToInt32(processed.Rows[0]["processed"]) == 3, processed.Rows[0]["processed"].ToString());

// Отчёты через ADO.NET
foreach (var (proc, withDates) in new[] { ("sp_ReportByCategory", true), ("sp_ReportByMonth", true), ("sp_ReportBudgetExecution", true), ("sp_ReportAccountBalances", false) })
{
    var ps = new List<SqlParameter> { new("@UserId", 2) };
    if (withDates) { ps.Add(new("@DateFrom", new DateTime(2026, 1, 1))); ps.Add(new("@DateTo", new DateTime(2026, 11, 4))); }
    var dt = Db.ExecuteProcedure(proc, ps.ToArray());
    Check(proc, dt.Rows.Count > 0, $"rows={dt.Rows.Count} cols=[{string.Join(", ", dt.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName + ":" + c.DataType.Name))}]");
}

// Фильтры страницы операций
using (var db = new FinanceContext())
{
    string search = "аренда";
    var q = db.Transactions.Include(t => t.Account).ThenInclude(a => a!.Currency).Include(t => t.Category)
        .Where(t => t.Account!.UserId == 2)
        .Where(t => t.Comment!.Contains(search) || t.Category!.Name.Contains(search));
    Check("search", q.Count() == 2);
    var from = new DateTime(2026, 10, 1); var to = new DateTime(2026, 10, 31);
    Check("period+type", db.Transactions.Count(t => t.Account!.UserId == 2 && t.Category!.Type == "Расход" && t.TransactionDate >= from && t.TransactionDate <= to) == 10);
}

// Удаление пользователя
Db.ExecuteProcedure("sp_DeleteUser", new SqlParameter("@UserId", newUserId));
using (var db = new FinanceContext()) Check("delete user", !db.Users.Any(u => u.Id == newUserId) && !db.Categories.Any(c => c.UserId == newUserId));


// Сообщения при недоступном сервере и несуществующей базе
static Exception OpenFails(string cs) { try { using var c = new SqlConnection(cs); c.Open(); return new Exception("подключение удалось"); } catch (Exception ex) { return ex; } }
Check("no server message", Db.ErrorMessage(OpenFails("Server=localhost,1499;Connect Timeout=2;TrustServerCertificate=True")).StartsWith("Нет подключения"));
Check("no database message", Db.ErrorMessage(OpenFails(new SqlConnectionStringBuilder(Db.ConnectionString) { InitialCatalog = "NoSuchDb" }.ConnectionString)).StartsWith("Нет подключения"));

Console.WriteLine(fails == 0 ? "ALL PASSED" : $"FAILED: {fails}");
