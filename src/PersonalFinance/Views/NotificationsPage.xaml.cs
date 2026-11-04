using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class NotificationsPage : UserControl
{
    private readonly MainWindow _main;

    public NotificationsPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        var query = db.Notifications.Where(n => n.UserId == Session.UserId);
        if (UnreadOnly.IsChecked == true)
            query = query.Where(n => !n.IsRead);
        Grid.ItemsSource = query.OrderByDescending(n => n.CreatedAt).ToList();
        _main.UpdateNotificationCounter();
    }

    private void MarkRead_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Notification n) { Ui.Warning("Выберите уведомление в таблице."); return; }
        Execute(db => db.Notifications.Where(x => x.Id == n.Id && x.UserId == Session.UserId)
            .ExecuteUpdate(s => s.SetProperty(x => x.IsRead, true)));
    }

    private void MarkAllRead_Click(object sender, RoutedEventArgs e) =>
        Execute(db => db.Notifications.Where(x => x.UserId == Session.UserId && !x.IsRead)
            .ExecuteUpdate(s => s.SetProperty(x => x.IsRead, true)));

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Notification n) { Ui.Warning("Выберите уведомление в таблице."); return; }
        if (!Ui.Confirm("Удалить выбранное уведомление?")) return;
        Execute(db => db.Notifications.Where(x => x.Id == n.Id && x.UserId == Session.UserId).ExecuteDelete());
    }

    private void Execute(Func<FinanceContext, int> action)
    {
        try
        {
            using var db = new FinanceContext();
            action(db);
            LoadData();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => LoadData();
}
