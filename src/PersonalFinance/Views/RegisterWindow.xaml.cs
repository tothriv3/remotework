using System.Windows;
using PersonalFinance.Data;

namespace PersonalFinance.Views;

public partial class RegisterWindow : Window
{
    public string RegisteredEmail { get; private set; } = "";

    public RegisterWindow()
    {
        InitializeComponent();
        NameBox.Focus();
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string email = EmailBox.Text.Trim().ToLowerInvariant();

        if (name.Length < 2) { Ui.Warning("Введите ФИО (не менее 2 символов)."); return; }
        if (!Ui.IsValidEmail(email)) { Ui.Warning("Введите корректный email, например name@mail.ru."); return; }
        if (!Ui.IsStrongPassword(PasswordBox.Password))
        { Ui.Warning("Пароль должен содержать не менее 8 символов, буквы и цифры."); return; }
        if (PasswordBox.Password != RepeatBox.Password) { Ui.Warning("Пароли не совпадают."); return; }

        try
        {
            using var db = new FinanceContext();
            if (db.Users.Any(u => u.Email == email))
            {
                Ui.Warning("Пользователь с таким email уже зарегистрирован.");
                return;
            }

            db.CreateUser(name, email, PasswordBox.Password, Session.UserRole);

            RegisteredEmail = email;
            Ui.Info("Регистрация выполнена. Войдите, используя свой email и пароль.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }
}
