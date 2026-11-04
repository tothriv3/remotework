using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class BudgetsPage : UserControl
{
    private int? _editId;

    public BudgetsPage()
    {
        InitializeComponent();
        using var db = new FinanceContext();
        CategoryBox.ItemsSource = db.Categories
            .Where(c => c.UserId == Session.UserId && c.Type == "Расход")
            .OrderBy(c => c.Name).ToList();
        PeriodBox.ItemsSource = Ui.Periods;
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        // данные берутся из представления vw_BudgetExecution
        var query = db.BudgetExecutions.Where(b => b.UserId == Session.UserId);
        if (ActiveOnly.IsChecked == true)
            query = query.Where(b => b.StartDate <= DateTime.Today && b.EndDate >= DateTime.Today);
        Grid.ItemsSource = query.OrderByDescending(b => b.StartDate).ThenBy(b => b.CategoryName).ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новый бюджет";
        CategoryBox.SelectedIndex = -1;
        LimitBox.Text = "";
        PeriodBox.SelectedItem = "Месяц";
        StartBox.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Period_Changed(this, new RoutedEventArgs());
    }

    // Дата окончания рассчитывается по периоду и дате начала
    private void Period_Changed(object sender, RoutedEventArgs e)
    {
        if (StartBox.SelectedDate is not DateTime start || PeriodBox.SelectedItem is not string period)
            return;

        EndBox.SelectedDate = period switch
        {
            "Неделя" => start.AddDays(7),
            "Месяц" => start.AddMonths(1),
            "Квартал" => start.AddMonths(3),
            _ => start.AddYears(1)
        } - TimeSpan.FromDays(1);
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not BudgetExecution row)
            return;

        using var db = new FinanceContext();
        var budget = db.Budgets.Single(b => b.Id == row.BudgetId);
        _editId = budget.Id;
        FormTitle.Text = "Изменение бюджета";
        CategoryBox.SelectedItem = ((IEnumerable<Category>)CategoryBox.ItemsSource).First(c => c.Id == budget.CategoryId);
        LimitBox.Text = budget.LimitAmount.ToString("0.00");
        PeriodBox.SelectedItem = budget.Period;
        StartBox.SelectedDate = budget.StartDate;
        EndBox.SelectedDate = budget.EndDate; // после автоматического пересчёта — сохранённое значение
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryBox.SelectedItem is not Category category) { Ui.Warning("Выберите категорию расходов."); return; }
        if (!Ui.TryParseAmount(LimitBox.Text, out decimal limit))
        { Ui.Warning("Лимит должен быть положительным числом (не более 2 знаков после запятой)."); return; }
        if (PeriodBox.SelectedItem is not string period) { Ui.Warning("Выберите период."); return; }
        if (StartBox.SelectedDate is not DateTime start || EndBox.SelectedDate is not DateTime end)
        { Ui.Warning("Укажите даты начала и окончания."); return; }
        if (end < start) { Ui.Warning("Дата окончания не может быть раньше даты начала."); return; }

        try
        {
            using var db = new FinanceContext();
            var budget = _editId == null
                ? db.Budgets.Add(new Budget { UserId = Session.UserId }).Entity
                : db.Budgets.Single(b => b.Id == _editId && b.UserId == Session.UserId);
            budget.CategoryId = category.Id;
            budget.LimitAmount = limit;
            budget.Period = period;
            budget.StartDate = start;
            budget.EndDate = end;
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
        if (Grid.SelectedItem is not BudgetExecution row) { Ui.Warning("Выберите бюджет в таблице."); return; }
        if (!Ui.Confirm($"Удалить бюджет «{row.CategoryName}» ({row.StartDate:dd.MM.yyyy} — {row.EndDate:dd.MM.yyyy})?")) return;

        try
        {
            using var db = new FinanceContext();
            db.Budgets.Where(b => b.Id == row.BudgetId && b.UserId == Session.UserId).ExecuteDelete();
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
