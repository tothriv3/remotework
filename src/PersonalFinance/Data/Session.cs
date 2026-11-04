using PersonalFinance.Models;

namespace PersonalFinance.Data;

// Текущий авторизованный пользователь
public static class Session
{
    public const string AdminRole = "Администратор";
    public const string UserRole = "Пользователь";

    public static User? CurrentUser { get; set; }

    public static int UserId => CurrentUser?.Id ?? throw new InvalidOperationException("Пользователь не авторизован.");

    public static bool IsAdmin => CurrentUser?.Role?.Name == AdminRole;
}
