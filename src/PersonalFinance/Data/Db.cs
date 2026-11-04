using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;

namespace PersonalFinance.Data;

// Подключение к БД, вызов хранимых процедур через ADO.NET и перевод ошибок SQL Server
public static class Db
{
    public static string ConnectionString =>
        ConfigurationManager.ConnectionStrings["FinanceDb"]?.ConnectionString
        ?? throw new InvalidOperationException("В App.config не найдена строка подключения FinanceDb.");

    // Выполняет хранимую процедуру и возвращает результат в виде таблицы
    public static DataTable ExecuteProcedure(string name, params SqlParameter[] parameters)
    {
        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand(name, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddRange(parameters);

        var table = new DataTable();
        connection.Open();
        using var reader = command.ExecuteReader();
        table.Load(reader);
        return table;
    }

    // Понятное пользователю сообщение об ошибке базы данных
    public static string ErrorMessage(Exception ex)
    {
        var sql = ex as SqlException ?? ex.InnerException as SqlException;
        if (sql == null)
            return ex.Message;

        return sql.Number switch
        {
            >= 50000 => sql.Errors[0].Message, // сообщения из триггеров уже на русском языке
            2627 or 2601 => "Такая запись уже существует (нарушено условие уникальности).",
            // текст ошибки 547 зависит от языка сервера, но имя инструкции
            // и имя ограничения (CK_..., FK_...) в нём есть всегда
            547 when sql.Message.Contains("DELETE") =>
                "Запись нельзя удалить: на неё ссылаются другие данные.",
            547 when sql.Message.Contains("CK_") =>
                "Данные не прошли проверку ограничений базы данных.",
            547 => "Связанная запись не найдена или принадлежит другому пользователю.",
            -2 => "Превышено время ожидания ответа от сервера базы данных.",
            0 or -1 or 2 or 53 or 4060 or 18456 =>
                "Нет подключения к базе данных. Проверьте строку подключения в файле App.config.",
            _ => sql.Message
        };
    }
}
