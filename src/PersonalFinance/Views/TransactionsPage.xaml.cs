using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class TransactionsPage : UserControl
{
    private const string AllTypes = "Все типы";
    private int? _editId;

    public TransactionsPage()
    {
        InitializeComponent();

        using var db = new FinanceContext();
        var accounts = db.Accounts.Include(a => a.Currency)
            .Where(a => a.UserId == Session.UserId).OrderBy(a => a.Name).ToList();
        AccountBox.ItemsSource = accounts;
        AccountFilter.ItemsSource = accounts.Prepend(new Account { Name = "Все счета" }).ToList();
        AccountFilter.SelectedIndex = 0;

        CategoryBox.ItemsSource = db.Categories.Where(c => c.UserId == Session.UserId)
            .OrderBy(c => c.Type).ThenBy(c => c.Name).ToList();
        TypeFilter.ItemsSource = Ui.CategoryTypes.Prepend(AllTypes).ToList();
        TypeFilter.SelectedIndex = 0;

        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        var query = db.Transactions
            .Include(t => t.Account).ThenInclude(a => a!.Currency)
            .Include(t => t.Category)
            .Where(t => t.Account!.UserId == Session.UserId);

        if (AccountFilter.SelectedItem is Account account && account.Id != 0)
            query = query.Where(t => t.AccountId == account.Id);
        if (TypeFilter.SelectedItem is string type && type != AllTypes)
            query = query.Where(t => t.Category!.Type == type);
        if (FromFilter.SelectedDate is DateTime from)
            query = query.Where(t => t.TransactionDate >= from);
        if (ToFilter.SelectedDate is DateTime to)
            query = query.Where(t => t.TransactionDate <= to);

        string search = SearchBox.Text.Trim();
        if (search.Length > 0)
            query = query.Where(t => t.Comment!.Contains(search) || t.Category!.Name.Contains(search));

        var list = query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id).ToList();
        Grid.ItemsSource = list;
        CountText.Text = $"Найдено операций: {list.Count}";
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новая операция";
        AccountBox.SelectedIndex = 0;
        CategoryBox.SelectedIndex = -1;
        AmountBox.Text = "";
        DateBox.SelectedDate = DateTime.Today;
        CommentBox.Text = "";
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not Transaction t)
            return;

        _editId = t.Id;
        FormTitle.Text = "Изменение операции";
        AccountBox.SelectedItem = ((IEnumerable<Account>)AccountBox.ItemsSource).First(a => a.Id == t.AccountId);
        CategoryBox.SelectedItem = ((IEnumerable<Category>)CategoryBox.ItemsSource).First(c => c.Id == t.CategoryId);
        AmountBox.Text = t.Amount.ToString("0.00");
        DateBox.SelectedDate = t.TransactionDate;
        CommentBox.Text = t.Comment ?? "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (AccountBox.SelectedItem is not Account account) { Ui.Warning("Выберите счёт."); return; }
        if (CategoryBox.SelectedItem is not Category category) { Ui.Warning("Выберите категорию."); return; }
        if (!Ui.TryParseAmount(AmountBox.Text, out decimal amount))
        { Ui.Warning("Сумма должна быть положительным числом (не более 2 знаков после запятой)."); return; }
        if (DateBox.SelectedDate is not DateTime date) { Ui.Warning("Укажите дату операции."); return; }

        try
        {
            using var db = new FinanceContext();
            var t = _editId == null
                ? db.Transactions.Add(new Transaction()).Entity
                : db.Transactions.Single(x => x.Id == _editId && x.Account!.UserId == Session.UserId);
            t.AccountId = account.Id;
            t.CategoryId = category.Id;
            t.Amount = amount;
            t.TransactionDate = date;
            t.Comment = string.IsNullOrWhiteSpace(CommentBox.Text) ? null : CommentBox.Text.Trim();
            db.SaveChanges(); // баланс счёта пересчитывает триггер в БД
            LoadData();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Transaction t) { Ui.Warning("Выберите операцию в таблице."); return; }
        if (!Ui.Confirm($"Удалить операцию от {t.TransactionDate:dd.MM.yyyy} на сумму {t.Amount:N2}?")) return;

        try
        {
            using var db = new FinanceContext();
            db.Transactions.Where(x => x.Id == t.Id && x.Account!.UserId == Session.UserId).ExecuteDelete();
            LoadData();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Grid.SelectedItem = null;
        ClearForm();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) LoadData();
    }
}
