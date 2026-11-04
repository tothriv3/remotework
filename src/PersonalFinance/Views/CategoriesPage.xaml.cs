using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Models;

namespace PersonalFinance.Views;

public partial class CategoriesPage : UserControl
{
    private const string AllTypes = "Все типы";
    private int? _editId;

    public CategoriesPage()
    {
        InitializeComponent();
        TypeBox.ItemsSource = Ui.CategoryTypes;
        TypeFilter.ItemsSource = Ui.CategoryTypes.Prepend(AllTypes).ToList();
        TypeFilter.SelectedIndex = 0;
        LoadData();
    }

    private void LoadData()
    {
        using var db = new FinanceContext();
        var query = db.Categories.Where(c => c.UserId == Session.UserId && c.Name.Contains(SearchBox.Text.Trim()));
        if (TypeFilter.SelectedItem is string type && type != AllTypes)
            query = query.Where(c => c.Type == type);
        Grid.ItemsSource = query.OrderBy(c => c.Type).ThenBy(c => c.Name).ToList();
        ClearForm();
    }

    private void ClearForm()
    {
        _editId = null;
        FormTitle.Text = "Новая категория";
        NameBox.Text = "";
        TypeBox.SelectedItem = "Расход";
        IconBox.Text = "";
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not Category category)
            return;

        _editId = category.Id;
        FormTitle.Text = "Изменение категории";
        NameBox.Text = category.Name;
        TypeBox.SelectedItem = category.Type;
        IconBox.Text = category.Icon ?? "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0) { Ui.Warning("Введите название категории."); return; }
        if (TypeBox.SelectedItem is not string type) { Ui.Warning("Выберите тип категории."); return; }

        try
        {
            using var db = new FinanceContext();
            var category = _editId == null
                ? db.Categories.Add(new Category { UserId = Session.UserId }).Entity
                : db.Categories.Single(c => c.Id == _editId && c.UserId == Session.UserId);
            category.Name = name;
            category.Type = type;
            category.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text.Trim();
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
        if (Grid.SelectedItem is not Category category) { Ui.Warning("Выберите категорию в таблице."); return; }
        if (!Ui.Confirm($"Удалить категорию «{category.Name}»?")) return;

        try
        {
            using var db = new FinanceContext();
            db.Categories.Where(c => c.Id == category.Id && c.UserId == Session.UserId).ExecuteDelete();
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
