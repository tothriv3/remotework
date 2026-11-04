using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class GoalsPage : UserControl
{
    private int? _editId;

    public GoalsPage()
    {
        InitializeComponent();
        using var db = new FinanceContext();
        // первый элемент списка означает «цель без привязки к счёту»
        AccountBox.ItemsSource = db.Accounts.Include(a => a.Currency)
            .Where(a => a.UserId == Session.UserId).OrderBy(a => a.Name).ToList()
            .Prepend(new Account { Name = "— без счёта —" }).ToList();
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        Grid.ItemsSource = db.Goals
            .Include(g => g.Account).ThenInclude(a => a!.Currency)
            .Where(g => g.UserId == Session.UserId)
            .OrderBy(g => g.Deadline == null).ThenBy(g => g.Deadline)
            .ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новая цель";
        NameBox.Text = "";
        AccountBox.SelectedIndex = 0;
        TargetBox.Text = "";
        CurrentBox.Text = "0";
        DeadlineBox.SelectedDate = null;
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not Goal goal)
            return;

        _editId = goal.Id;
        FormTitle.Text = "Изменение цели";
        NameBox.Text = goal.Name;
        AccountBox.SelectedItem = ((IEnumerable<Account>)AccountBox.ItemsSource).First(a => a.Id == (goal.AccountId ?? 0));
        TargetBox.Text = goal.TargetAmount.ToString("0.00");
        CurrentBox.Text = goal.CurrentAmount.ToString("0.00");
        DeadlineBox.SelectedDate = goal.Deadline;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0) { Ui.Warning("Введите название цели."); return; }
        if (!Ui.TryParseAmount(TargetBox.Text, out decimal target))
        { Ui.Warning("Целевая сумма должна быть положительным числом."); return; }
        if (!Ui.TryParseAmount(CurrentBox.Text, out decimal current, allowZero: true))
        { Ui.Warning("Накопленная сумма должна быть неотрицательным числом."); return; }
        if (_editId == null && DeadlineBox.SelectedDate < DateTime.Today)
        { Ui.Warning("Срок новой цели не может быть в прошлом."); return; }

        try
        {
            using var db = new FinanceContext();
            var goal = _editId == null
                ? db.Goals.Add(new Goal { UserId = Session.UserId }).Entity
                : db.Goals.Single(g => g.Id == _editId && g.UserId == Session.UserId);
            goal.Name = name;
            goal.AccountId = AccountBox.SelectedItem is Account { Id: > 0 } account ? account.Id : null;
            goal.TargetAmount = target;
            goal.CurrentAmount = current;
            goal.Deadline = DeadlineBox.SelectedDate;
            db.SaveChanges();
            LoadData();

            if (current >= target)
                Ui.Info($"Поздравляем! Цель «{name}» достигнута.");
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Goal goal) { Ui.Warning("Выберите цель в таблице."); return; }
        if (!Ui.Confirm($"Удалить цель «{goal.Name}»?")) return;

        try
        {
            using var db = new FinanceContext();
            db.Goals.Where(g => g.Id == goal.Id && g.UserId == Session.UserId).ExecuteDelete();
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
