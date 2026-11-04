using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

// Справочник валют (доступен только администратору)
public partial class CurrenciesPage : UserControl
{
    private int? _editId;

    public CurrenciesPage()
    {
        InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        Grid.ItemsSource = db.Currencies.OrderBy(c => c.Code).ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новая валюта";
        CodeBox.Text = "";
        NameBox.Text = "";
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not Currency currency)
            return;

        _editId = currency.Id;
        FormTitle.Text = "Изменение валюты";
        CodeBox.Text = currency.Code;
        NameBox.Text = currency.Name;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        string name = NameBox.Text.Trim();
        if (!Regex.IsMatch(code, "^[A-Z]{3}$")) { Ui.Warning("Код валюты — три латинские буквы, например RUB."); return; }
        if (name.Length == 0) { Ui.Warning("Введите название валюты."); return; }

        try
        {
            using var db = new FinanceContext();
            var currency = _editId == null
                ? db.Currencies.Add(new Currency()).Entity
                : db.Currencies.Single(c => c.Id == _editId);
            currency.Code = code;
            currency.Name = name;
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
        if (Grid.SelectedItem is not Currency currency) { Ui.Warning("Выберите валюту в таблице."); return; }
        if (!Ui.Confirm($"Удалить валюту {currency.Code}?")) return;

        try
        {
            using var db = new FinanceContext();
            db.Currencies.Where(c => c.Id == currency.Id).ExecuteDelete();
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
