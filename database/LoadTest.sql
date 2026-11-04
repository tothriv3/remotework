/* ==========================================================================
   Нагрузочное тестирование базы данных PersonalFinanceDB
   1) создаёт тестового пользователя и 100 000 операций;
   2) замеряет время вставки и время выполнения отчётов;
   3) удаляет тестовые данные.
   Запускать после PersonalFinanceDB.sql.
   ========================================================================== */

USE PersonalFinanceDB;
GO
SET NOCOUNT ON;

DECLARE @UserId int, @Acc1 int, @Acc2 int, @t datetime2;

INSERT INTO users (role_id, full_name, email, password_hash)
VALUES (2, N'Нагрузочный тест', N'loadtest@test.ru', N'-');
SET @UserId = SCOPE_IDENTITY();

EXEC sp_CreateDefaultCategories @UserId = @UserId;

INSERT INTO accounts (user_id, currency_id, name, balance) VALUES (@UserId, 1, N'Счёт 1', 0);
SET @Acc1 = SCOPE_IDENTITY();
INSERT INTO accounts (user_id, currency_id, name, balance) VALUES (@UserId, 1, N'Счёт 2', 0);
SET @Acc2 = SCOPE_IDENTITY();

-- 100 000 операций за 2 года: случайные счёт, категория, сумма и дата
SET @t = SYSDATETIME();

WITH n AS (
    SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO transactions (account_id, category_id, amount, transaction_date, comment)
SELECT CASE WHEN n.i % 2 = 0 THEN @Acc1 ELSE @Acc2 END,
       c.id,
       CAST(100 + ABS(CHECKSUM(NEWID())) % 10000 AS decimal(18, 2)),
       DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 730), CAST('2026-11-04' AS date)),
       N'Тестовая операция'
FROM n
CROSS APPLY (SELECT id FROM categories
             WHERE user_id = @UserId
             ORDER BY id
             OFFSET (n.i % 12) ROWS FETCH NEXT 1 ROWS ONLY) c;

PRINT CONCAT(N'Вставка 100 000 операций, мс: ', DATEDIFF(MILLISECOND, @t, SYSDATETIME()));

-- Проверка: баланс, рассчитанный триггером, совпадает с суммой операций
SELECT a.name AS account,
       a.balance AS balance_by_trigger,
       (SELECT SUM(CASE c.type WHEN N'Доход' THEN t.amount ELSE -t.amount END)
        FROM transactions t JOIN categories c ON c.id = t.category_id
        WHERE t.account_id = a.id) AS balance_by_sum
FROM accounts a
WHERE a.user_id = @UserId;

-- Замер отчётов (чтение из кэша после первого запуска)
SET @t = SYSDATETIME();
EXEC sp_ReportByCategory @UserId, '2026-01-01', '2026-12-31';
PRINT CONCAT(N'Отчёт по категориям за год, мс: ', DATEDIFF(MILLISECOND, @t, SYSDATETIME()));

SET @t = SYSDATETIME();
EXEC sp_ReportByMonth @UserId, '2024-11-01', '2026-11-30';
PRINT CONCAT(N'Отчёт по месяцам за 2 года, мс: ', DATEDIFF(MILLISECOND, @t, SYSDATETIME()));

-- Типичный запрос клиентского приложения: операции счёта за месяц
SET @t = SYSDATETIME();
SELECT COUNT(*) AS operations_in_month
FROM transactions
WHERE account_id = @Acc1 AND transaction_date BETWEEN '2026-10-01' AND '2026-10-31';
PRINT CONCAT(N'Выборка операций счёта за месяц, мс: ', DATEDIFF(MILLISECOND, @t, SYSDATETIME()));

-- Удаление тестовых данных
SET @t = SYSDATETIME();
EXEC sp_DeleteUser @UserId;
PRINT CONCAT(N'Удаление пользователя и 100 000 операций, мс: ', DATEDIFF(MILLISECOND, @t, SYSDATETIME()));
GO
