using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

// Управление пользователями (доступно только администратору)
public partial class UsersPage : UserControl
{
    private int? _editId;

    public UsersPage()
    {
        InitializeComponent();
        using var db = new FinanceContext();
        RoleBox.ItemsSource = db.Roles.OrderBy(r => r.Id).ToList();
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        string search = SearchBox.Text.Trim();
        Grid.ItemsSource = db.Users.Include(u => u.Role)
            .Where(u => u.FullName.Contains(search) || u.Email.Contains(search))
            .OrderBy(u => u.FullName)
            .ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новый пользователь";
        NameBox.Text = "";
        EmailBox.Text = "";
        RoleBox.SelectedItem = ((IEnumerable<Role>)RoleBox.ItemsSource).First(r => r.Name == Session.UserRole);
        PasswordLabel.Text = "Пароль";
        PasswordBox.Clear();
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not User user)
            return;

        _editId = user.Id;
        FormTitle.Text = "Изменение пользователя";
        NameBox.Text = user.FullName;
        EmailBox.Text = user.Email;
        RoleBox.SelectedItem = ((IEnumerable<Role>)RoleBox.ItemsSource).First(r => r.Id == user.RoleId);
        PasswordLabel.Text = "Новый пароль (оставьте пустым, чтобы не менять)";
        PasswordBox.Clear();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string email = EmailBox.Text.Trim().ToLowerInvariant();
        string password = PasswordBox.Password;
        var role = (Role)RoleBox.SelectedItem;

        if (name.Length < 2) { Ui.Warning("Введите ФИО (не менее 2 символов)."); return; }
        if (!Ui.IsValidEmail(email)) { Ui.Warning("Введите корректный email, например name@mail.ru."); return; }
        if ((_editId == null || password.Length > 0) && !Ui.IsStrongPassword(password))
        { Ui.Warning("Пароль должен содержать не менее 8 символов, буквы и цифры."); return; }
        if (_editId == Session.UserId && role.Name != Session.AdminRole)
        { Ui.Warning("Нельзя снять роль администратора с самого себя."); return; }

        try
        {
            using var db = new FinanceContext();
            if (db.Users.Any(u => u.Email == email && u.Id != _editId))
            { Ui.Warning("Пользователь с таким email уже существует."); return; }

            if (_editId == null)
            {
                db.CreateUser(name, email, password, role.Name);
            }
            else
            {
                var user = db.Users.Single(u => u.Id == _editId);
                user.FullName = name;
                user.Email = email;
                user.RoleId = role.Id;
                if (password.Length > 0)
                    user.PasswordHash = PasswordHasher.Hash(password);
                db.SaveChanges();
            }
            LoadData();
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not User user) { Ui.Warning("Выберите пользователя в таблице."); return; }
        if (user.Id == Session.UserId) { Ui.Warning("Нельзя удалить собственную учётную запись."); return; }
        if (!Ui.Confirm($"Удалить пользователя «{user.FullName}» и все его данные?")) return;

        try
        {
            // процедура удаляет все данные пользователя в одной транзакции
            Db.ExecuteProcedure("sp_DeleteUser", new SqlParameter("@UserId", user.Id));
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
