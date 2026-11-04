using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class RecurringPage : UserControl
{
    private int? _editId;

    public RecurringPage()
    {
        InitializeComponent();
        using var db = new FinanceContext();
        AccountBox.ItemsSource = db.Accounts.Include(a => a.Currency)
            .Where(a => a.UserId == Session.UserId).OrderBy(a => a.Name).ToList();
        CategoryBox.ItemsSource = db.Categories.Where(c => c.UserId == Session.UserId)
            .OrderBy(c => c.Type).ThenBy(c => c.Name).ToList();
        FrequencyBox.ItemsSource = Ui.Frequencies;
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        Grid.ItemsSource = db.RecurringPayments
            .Include(r => r.Account).ThenInclude(a => a!.Currency)
            .Include(r => r.Category)
            .Where(r => r.Account!.UserId == Session.UserId)
            .OrderBy(r => r.NextPaymentDate)
            .ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новый платёж";
        AccountBox.SelectedIndex = 0;
        CategoryBox.SelectedIndex = -1;
        AmountBox.Text = "";
        FrequencyBox.SelectedItem = "Ежемесячно";
        NextDateBox.SelectedDate = DateTime.Today.AddDays(1);
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not RecurringPayment r)
            return;

        _editId = r.Id;
        FormTitle.Text = "Изменение платежа";
        AccountBox.SelectedItem = ((IEnumerable<Account>)AccountBox.ItemsSource).First(a => a.Id == r.AccountId);
        CategoryBox.SelectedItem = ((IEnumerable<Category>)CategoryBox.ItemsSource).First(c => c.Id == r.CategoryId);
        AmountBox.Text = r.Amount.ToString("0.00");
        FrequencyBox.SelectedItem = r.Frequency;
        NextDateBox.SelectedDate = r.NextPaymentDate;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (AccountBox.SelectedItem is not Account account) { Ui.Warning("Выберите счёт."); return; }
        if (CategoryBox.SelectedItem is not Category category) { Ui.Warning("Выберите категорию."); return; }
        if (!Ui.TryParseAmount(AmountBox.Text, out decimal amount))
        { Ui.Warning("Сумма должна быть положительным числом (не более 2 знаков после запятой)."); return; }
        if (FrequencyBox.SelectedItem is not string frequency) { Ui.Warning("Выберите периодичность."); return; }
        if (NextDateBox.SelectedDate is not DateTime nextDate) { Ui.Warning("Укажите дату следующего платежа."); return; }

        try
        {
            using var db = new FinanceContext();
            var r = _editId == null
                ? db.RecurringPayments.Add(new RecurringPayment()).Entity
                : db.RecurringPayments.Single(x => x.Id == _editId && x.Account!.UserId == Session.UserId);
            r.AccountId = account.Id;
            r.CategoryId = category.Id;
            r.Amount = amount;
            r.Frequency = frequency;
            r.NextPaymentDate = nextDate;
            db.SaveChanges();
            LoadData();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RecurringPayment r) { Ui.Warning("Выберите платёж в таблице."); return; }
        if (!Ui.Confirm($"Удалить регулярный платёж «{r.Category?.Name}» на сумму {r.Amount:N2}?")) return;

        try
        {
            using var db = new FinanceContext();
            db.RecurringPayments.Where(x => x.Id == r.Id && x.Account!.UserId == Session.UserId).ExecuteDelete();
            LoadData();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    // Проведение платежей, срок которых наступил (хранимая процедура через ADO.NET)
    private void Process_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = Db.ExecuteProcedure("sp_ProcessRecurringPayments", new SqlParameter("@UserId", Session.UserId));
            Ui.Info($"Проведено платежей: {result.Rows[0]["processed"]}.");
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
}
