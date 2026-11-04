using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class AccountsPage : UserControl
{
    private int? _editId; // null — добавление новой записи

    public AccountsPage()
    {
        InitializeComponent();
        using var db = new FinanceContext();
        CurrencyBox.ItemsSource = db.Currencies.OrderBy(c => c.Id).ToList(); // первой идёт RUB
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        string search = SearchBox.Text.Trim();
        Grid.ItemsSource = db.Accounts
            .Include(a => a.Currency)
            .Where(a => a.UserId == Session.UserId && a.Name.Contains(search))
            .OrderBy(a => a.Name)
            .ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новый счёт";
        NameBox.Text = "";
        CurrencyBox.SelectedIndex = 0;
        CurrencyBox.IsEnabled = true;
        BalanceBox.Text = "0";
        BalanceBox.IsEnabled = true;
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not Account account)
            return;

        _editId = account.Id;
        FormTitle.Text = "Изменение счёта";
        NameBox.Text = account.Name;
        CurrencyBox.SelectedItem = ((IEnumerable<Currency>)CurrencyBox.ItemsSource).First(c => c.Id == account.CurrencyId);
        BalanceBox.Text = account.Balance.ToString("N2");
        // валюта и баланс существующего счёта меняются только через операции
        CurrencyBox.IsEnabled = false;
        BalanceBox.IsEnabled = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0) { Ui.Warning("Введите название счёта."); return; }
        if (CurrencyBox.SelectedItem is not Currency currency) { Ui.Warning("Выберите валюту."); return; }
        if (!Ui.TryParseAmount(BalanceBox.Text, out decimal balance, allowZero: true))
        { Ui.Warning("Начальный баланс должен быть неотрицательным числом (не более 2 знаков после запятой)."); return; }

        try
        {
            using var db = new FinanceContext();
            if (_editId == null)
                db.Accounts.Add(new Account { UserId = Session.UserId, CurrencyId = currency.Id, Name = name, Balance = balance });
            else
                db.Accounts.Single(a => a.Id == _editId && a.UserId == Session.UserId).Name = name;
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
        if (Grid.SelectedItem is not Account account) { Ui.Warning("Выберите счёт в таблице."); return; }
        if (!Ui.Confirm($"Удалить счёт «{account.Name}»?")) return;

        try
        {
            using var db = new FinanceContext();
            db.Accounts.Where(a => a.Id == account.Id && a.UserId == Session.UserId).ExecuteDelete();
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

    private void Filter_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) LoadData();
    }
}
