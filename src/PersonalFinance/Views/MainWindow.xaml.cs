using System.Windows;
using System.Windows.Controls;
using PersonalFinance.Data;

namespace PersonalFinance.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        UserText.Text = Session.CurrentUser!.FullName;
        RoleText.Text = Session.CurrentUser.Role!.Name;

        // Разделы администратора скрыты от обычных пользователей
        if (!Session.IsAdmin)
        {
            UsersItem.Visibility = Visibility.Collapsed;
            CurrenciesItem.Visibility = Visibility.Collapsed;
        }

        Menu.SelectedIndex = 0;
    }

    private void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Menu.SelectedItem is not ListBoxItem item)
            return;

        try
        {
            PageHost.Content = (string)item.Tag switch
            {
                "Accounts" => new AccountsPage(),
                "Transactions" => new TransactionsPage(),
                "Categories" => new CategoriesPage(),
                "Budgets" => new BudgetsPage(),
                "Goals" => new GoalsPage(),
                "Recurring" => new RecurringPage(),
                "Notifications" => new NotificationsPage(this),
                "Reports" => new ReportsPage(),
                "Users" => new UsersPage(),
                "Currencies" => new CurrenciesPage(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }

        UpdateNotificationCounter();
    }

    // Количество непрочитанных уведомлений в пункте меню
    public void UpdateNotificationCounter()
    {
        try
        {
            using var db = new FinanceContext();
            int unread = db.Notifications.Count(n => n.UserId == Session.UserId && !n.IsRead);
            NotificationsItem.Content = unread > 0 ? $"Уведомления ({unread})" : "Уведомления";
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        Session.CurrentUser = null;
        new LoginWindow().Show();
        Close();
    }
}
