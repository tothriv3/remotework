using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using PersonalFinance.Data;

namespace PersonalFinance;

// Общие диалоги и проверка ввода
public static class Ui
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    // Допустимые значения, совпадающие с ограничениями CHECK в базе данных
    public static readonly string[] CategoryTypes = { "Доход", "Расход" };
    public static readonly string[] Periods = { "Неделя", "Месяц", "Квартал", "Год" };
    public static readonly string[] Frequencies = { "Ежедневно", "Еженедельно", "Ежемесячно", "Ежегодно" };

    public static void Info(string text) =>
        MessageBox.Show(text, "Личный бюджет", MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Warning(string text) =>
        MessageBox.Show(text, "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Error(Exception ex) =>
        MessageBox.Show(Db.ErrorMessage(ex), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool Confirm(string text) =>
        MessageBox.Show(text, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    // Сумма: число больше нуля (или не меньше нуля), не более двух знаков после запятой
    public static bool TryParseAmount(string text, out decimal value, bool allowZero = false)
    {
        return decimal.TryParse(text.Trim().Replace('.', ','), NumberStyles.Number, Ru, out value)
               && (allowZero ? value >= 0 : value > 0)
               && value == Math.Round(value, 2)
               && value < 10_000_000_000_000_000m; // предел типа decimal(18, 2)
    }

    public static bool IsValidEmail(string email) =>
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    // Пароль: не менее 8 символов, хотя бы одна буква и одна цифра
    public static bool IsStrongPassword(string password) =>
        password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit);
}
