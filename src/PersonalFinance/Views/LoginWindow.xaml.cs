using System.Windows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;

namespace PersonalFinance.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        EmailBox.Focus();
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        string email = EmailBox.Text.Trim();
        if (email.Length == 0) { Ui.Warning("Введите email."); EmailBox.Focus(); return; }
        if (PasswordBox.Password.Length == 0) { Ui.Warning("Введите пароль."); PasswordBox.Focus(); return; }

        try
        {
            using var db = new FinanceContext();
            var user = db.Users.Include(u => u.Role).FirstOrDefault(u => u.Email == email);
            if (user == null || !PasswordHasher.Verify(PasswordBox.Password, user.PasswordHash))
            {
                Ui.Warning("Неверный email или пароль.");
                PasswordBox.Clear();
                PasswordBox.Focus();
                return;
            }

            Session.CurrentUser = user;

            // Проведение регулярных платежей, срок которых наступил (ADO.NET + хранимая процедура)
            Db.ExecuteProcedure("sp_ProcessRecurringPayments", new SqlParameter("@UserId", user.Id));

            new MainWindow().Show();
            Close();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        var window = new RegisterWindow { Owner = this };
        if (window.ShowDialog() == true)
        {
            EmailBox.Text = window.RegisteredEmail;
            PasswordBox.Focus();
        }
    }
}
