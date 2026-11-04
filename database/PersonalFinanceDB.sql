/* ==========================================================================
   База данных сервиса по управлению личными финансами и бюджетом
   СУБД: Microsoft SQL Server 2019/2022 (в том числе Express)
   Скрипт можно запускать повторно: существующая база пересоздаётся.
   ========================================================================== */

USE master;
GO

IF DB_ID(N'PersonalFinanceDB') IS NOT NULL
BEGIN
    ALTER DATABASE PersonalFinanceDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PersonalFinanceDB;
END
GO

CREATE DATABASE PersonalFinanceDB COLLATE Cyrillic_General_CI_AS;
GO

USE PersonalFinanceDB;
GO

/* ==========================================================================
   1. ТАБЛИЦЫ
   ========================================================================== */

-- Роли пользователей системы
CREATE TABLE roles (
    id   int IDENTITY(1, 1) CONSTRAINT PK_roles PRIMARY KEY,
    name nvarchar(50) NOT NULL CONSTRAINT UQ_roles_name UNIQUE
);
GO

-- Пользователи сервиса
CREATE TABLE users (
    id            int IDENTITY(1, 1) CONSTRAINT PK_users PRIMARY KEY,
    role_id       int           NOT NULL
        CONSTRAINT FK_users_roles REFERENCES roles (id),
    full_name     nvarchar(150) NOT NULL,
    email         nvarchar(255) NOT NULL CONSTRAINT UQ_users_email UNIQUE,
    password_hash nvarchar(255) NOT NULL,
    created_at    datetime      NOT NULL CONSTRAINT DF_users_created_at DEFAULT GETDATE(),
    CONSTRAINT CK_users_full_name CHECK (LEN(full_name) >= 2),
    CONSTRAINT CK_users_email     CHECK (email LIKE N'%_@_%._%')
);
GO

-- Справочник валют (код по ISO 4217)
CREATE TABLE currencies (
    id   int IDENTITY(1, 1) CONSTRAINT PK_currencies PRIMARY KEY,
    code nchar(3)     NOT NULL CONSTRAINT UQ_currencies_code UNIQUE,
    name nvarchar(50) NOT NULL,
    CONSTRAINT CK_currencies_code CHECK (code COLLATE Latin1_General_BIN LIKE N'[A-Z][A-Z][A-Z]')
);
GO

-- Счета пользователя (карты, наличные, вклады)
CREATE TABLE accounts (
    id          int IDENTITY(1, 1) CONSTRAINT PK_accounts PRIMARY KEY,
    user_id     int           NOT NULL
        CONSTRAINT FK_accounts_users REFERENCES users (id),
    currency_id int           NOT NULL
        CONSTRAINT FK_accounts_currencies REFERENCES currencies (id),
    name        nvarchar(100) NOT NULL,
    balance     decimal(18, 2) NOT NULL CONSTRAINT DF_accounts_balance DEFAULT 0,
    CONSTRAINT UQ_accounts_user_name UNIQUE (user_id, name),
    -- нужен для составного внешнего ключа из таблицы goals
    CONSTRAINT UQ_accounts_id_user   UNIQUE (id, user_id)
);
GO

-- Категории доходов и расходов
CREATE TABLE categories (
    id      int IDENTITY(1, 1) CONSTRAINT PK_categories PRIMARY KEY,
    user_id int           NOT NULL
        CONSTRAINT FK_categories_users REFERENCES users (id),
    name    nvarchar(100) NOT NULL,
    type    nvarchar(10)  NOT NULL,
    icon    nvarchar(10)  NULL,
    CONSTRAINT CK_categories_type           CHECK (type IN (N'Доход', N'Расход')),
    CONSTRAINT UQ_categories_user_name_type UNIQUE (user_id, name, type),
    -- нужен для составного внешнего ключа из таблицы budgets
    CONSTRAINT UQ_categories_id_user        UNIQUE (id, user_id)
);
GO

-- Финансовые операции. Тип операции (доход или расход) определяется категорией.
CREATE TABLE transactions (
    id               int IDENTITY(1, 1) CONSTRAINT PK_transactions PRIMARY KEY,
    account_id       int            NOT NULL
        CONSTRAINT FK_transactions_accounts REFERENCES accounts (id),
    category_id      int            NOT NULL
        CONSTRAINT FK_transactions_categories REFERENCES categories (id),
    amount           decimal(18, 2) NOT NULL,
    transaction_date date           NOT NULL
        CONSTRAINT DF_transactions_date DEFAULT CAST(GETDATE() AS date),
    comment          nvarchar(255)  NULL,
    CONSTRAINT CK_transactions_amount CHECK (amount > 0)
);
GO

-- Бюджеты: лимит расходов по категории на период
CREATE TABLE budgets (
    id           int IDENTITY(1, 1) CONSTRAINT PK_budgets PRIMARY KEY,
    user_id      int            NOT NULL
        CONSTRAINT FK_budgets_users REFERENCES users (id),
    category_id  int            NOT NULL,
    limit_amount decimal(18, 2) NOT NULL,
    period       nvarchar(10)   NOT NULL,
    start_date   date           NOT NULL,
    end_date     date           NOT NULL,
    -- категория бюджета обязана принадлежать тому же пользователю
    CONSTRAINT FK_budgets_categories FOREIGN KEY (category_id, user_id)
        REFERENCES categories (id, user_id),
    CONSTRAINT CK_budgets_limit  CHECK (limit_amount > 0),
    CONSTRAINT CK_budgets_period CHECK (period IN (N'Неделя', N'Месяц', N'Квартал', N'Год')),
    CONSTRAINT CK_budgets_dates  CHECK (end_date >= start_date),
    -- нужен для составного внешнего ключа из таблицы notifications
    CONSTRAINT UQ_budgets_id_user UNIQUE (id, user_id)
);
GO

-- Уведомления пользователю (о превышении бюджета, о проведённых платежах)
CREATE TABLE notifications (
    id         int IDENTITY(1, 1) CONSTRAINT PK_notifications PRIMARY KEY,
    user_id    int           NOT NULL
        CONSTRAINT FK_notifications_users REFERENCES users (id),
    budget_id  int           NULL,
    message    nvarchar(255) NOT NULL,
    is_read    bit           NOT NULL CONSTRAINT DF_notifications_is_read DEFAULT 0,
    created_at datetime      NOT NULL CONSTRAINT DF_notifications_created_at DEFAULT GETDATE(),
    -- при удалении бюджета его уведомления удаляются автоматически
    CONSTRAINT FK_notifications_budgets FOREIGN KEY (budget_id, user_id)
        REFERENCES budgets (id, user_id) ON DELETE CASCADE
);
GO

-- Финансовые цели (накопления)
CREATE TABLE goals (
    id             int IDENTITY(1, 1) CONSTRAINT PK_goals PRIMARY KEY,
    user_id        int            NOT NULL
        CONSTRAINT FK_goals_users REFERENCES users (id),
    account_id     int            NULL,
    name           nvarchar(100)  NOT NULL,
    target_amount  decimal(18, 2) NOT NULL,
    current_amount decimal(18, 2) NOT NULL CONSTRAINT DF_goals_current DEFAULT 0,
    deadline       date           NULL,
    -- счёт цели обязан принадлежать тому же пользователю
    CONSTRAINT FK_goals_accounts FOREIGN KEY (account_id, user_id)
        REFERENCES accounts (id, user_id),
    CONSTRAINT CK_goals_target  CHECK (target_amount > 0),
    CONSTRAINT CK_goals_current CHECK (current_amount >= 0)
);
GO

-- Регулярные (повторяющиеся) платежи и поступления
CREATE TABLE recurring_payments (
    id                int IDENTITY(1, 1) CONSTRAINT PK_recurring_payments PRIMARY KEY,
    account_id        int            NOT NULL
        CONSTRAINT FK_recurring_accounts REFERENCES accounts (id),
    category_id       int            NOT NULL
        CONSTRAINT FK_recurring_categories REFERENCES categories (id),
    amount            decimal(18, 2) NOT NULL,
    frequency         nvarchar(20)   NOT NULL,
    next_payment_date date           NOT NULL,
    CONSTRAINT CK_recurring_amount    CHECK (amount > 0),
    CONSTRAINT CK_recurring_frequency CHECK (frequency IN
        (N'Ежедневно', N'Еженедельно', N'Ежемесячно', N'Ежегодно'))
);
GO

/* ==========================================================================
   2. ИНДЕКСЫ (для внешних ключей и частых запросов)
   ========================================================================== */

CREATE INDEX IX_users_role_id ON users (role_id);
CREATE INDEX IX_accounts_currency_id ON accounts (currency_id);
CREATE INDEX IX_transactions_account_date
    ON transactions (account_id, transaction_date) INCLUDE (category_id, amount);
CREATE INDEX IX_transactions_category_date
    ON transactions (category_id, transaction_date) INCLUDE (amount);
CREATE INDEX IX_budgets_user_id ON budgets (user_id);
CREATE INDEX IX_budgets_category_id ON budgets (category_id);
CREATE INDEX IX_notifications_user_read ON notifications (user_id, is_read);
CREATE INDEX IX_notifications_budget_id ON notifications (budget_id);
CREATE INDEX IX_goals_user_id ON goals (user_id);
CREATE INDEX IX_goals_account_id ON goals (account_id);
CREATE INDEX IX_recurring_account_id ON recurring_payments (account_id);
CREATE INDEX IX_recurring_category_id ON recurring_payments (category_id);
GO

/* ==========================================================================
   3. ТРИГГЕРЫ
   ========================================================================== */

-- Проверка владельца и пересчёт баланса счёта при любом изменении операций
CREATE TRIGGER trg_transactions_balance
ON transactions
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN accounts a   ON a.id = i.account_id
               JOIN categories c ON c.id = i.category_id
               WHERE a.user_id <> c.user_id)
        THROW 50001, N'Счёт и категория операции принадлежат разным пользователям.', 1;

    WITH delta AS (
        SELECT i.account_id,
               CASE c.type WHEN N'Доход' THEN i.amount ELSE -i.amount END AS value
        FROM inserted i
        JOIN categories c ON c.id = i.category_id
        UNION ALL
        SELECT d.account_id,
               CASE c.type WHEN N'Доход' THEN -d.amount ELSE d.amount END
        FROM deleted d
        JOIN categories c ON c.id = d.category_id
    )
    UPDATE a
    SET balance = a.balance + s.total
    FROM accounts a
    JOIN (SELECT account_id, SUM(value) AS total
          FROM delta
          GROUP BY account_id) s ON s.account_id = a.id;
END
GO

-- Уведомление при превышении лимита бюджета (одно на каждый бюджет)
CREATE TRIGGER trg_transactions_budget_notify
ON transactions
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO notifications (user_id, budget_id, message)
    SELECT b.user_id,
           b.id,
           CONCAT(N'Превышен бюджет «', c.name, N'» (', b.period, N'): потрачено ',
                  FORMAT(s.spent, N'N2', N'ru-RU'), N' из ',
                  FORMAT(b.limit_amount, N'N2', N'ru-RU'))
    FROM budgets b
    JOIN categories c ON c.id = b.category_id
    CROSS APPLY (SELECT SUM(t.amount) AS spent
                 FROM transactions t
                 WHERE t.category_id = b.category_id
                   AND t.transaction_date BETWEEN b.start_date AND b.end_date) s
    WHERE EXISTS (SELECT 1 FROM inserted i
                  WHERE i.category_id = b.category_id
                    AND i.transaction_date BETWEEN b.start_date AND b.end_date)
      AND s.spent > b.limit_amount
      AND NOT EXISTS (SELECT 1 FROM notifications n WHERE n.budget_id = b.id);
END
GO

-- Запрет смены типа категории, если по ней уже есть операции или бюджеты
-- (иначе пересчёт баланса при удалении старых операций будет неверным)
CREATE TRIGGER trg_categories_type_lock
ON categories
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE(type) AND EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.id = i.id
        WHERE i.type <> d.type
          AND (EXISTS (SELECT 1 FROM transactions t WHERE t.category_id = i.id)
               OR EXISTS (SELECT 1 FROM budgets b WHERE b.category_id = i.id)))
        THROW 50002, N'Нельзя изменить тип категории, по которой уже есть операции или бюджеты.', 1;
END
GO

-- Бюджет можно установить только на категорию расходов
CREATE TRIGGER trg_budgets_expense_only
ON budgets
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN categories c ON c.id = i.category_id
               WHERE c.type <> N'Расход')
        THROW 50004, N'Бюджет можно установить только на категорию расходов.', 1;
END
GO

-- Счёт и категория регулярного платежа должны принадлежать одному пользователю
CREATE TRIGGER trg_recurring_owner
ON recurring_payments
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN accounts a   ON a.id = i.account_id
               JOIN categories c ON c.id = i.category_id
               WHERE a.user_id <> c.user_id)
        THROW 50003, N'Счёт и категория платежа принадлежат разным пользователям.', 1;
END
GO

/* ==========================================================================
   4. ПРЕДСТАВЛЕНИЯ
   ========================================================================== */

-- Операции с данными счёта, категории и валюты
CREATE VIEW vw_UserTransactions
AS
SELECT t.id,
       a.user_id,
       t.transaction_date,
       c.type,
       c.name  AS category_name,
       a.name  AS account_name,
       cur.code AS currency_code,
       t.amount,
       t.comment
FROM transactions t
JOIN accounts a     ON a.id = t.account_id
JOIN categories c   ON c.id = t.category_id
JOIN currencies cur ON cur.id = a.currency_id;
GO

-- Исполнение бюджетов: сколько потрачено и сколько осталось
CREATE VIEW vw_BudgetExecution
AS
SELECT b.id AS budget_id,
       b.user_id,
       c.name AS category_name,
       b.period,
       b.start_date,
       b.end_date,
       b.limit_amount,
       ISNULL(s.spent, 0) AS spent,
       b.limit_amount - ISNULL(s.spent, 0) AS remaining,
       CAST(ISNULL(s.spent, 0) * 100.0 / b.limit_amount AS decimal(7, 2)) AS percent_used
FROM budgets b
JOIN categories c ON c.id = b.category_id
OUTER APPLY (SELECT SUM(t.amount) AS spent
             FROM transactions t
             WHERE t.category_id = b.category_id
               AND t.transaction_date BETWEEN b.start_date AND b.end_date) s;
GO

/* ==========================================================================
   5. ХРАНИМЫЕ ПРОЦЕДУРЫ
   ========================================================================== */

-- Набор стандартных категорий для нового пользователя
CREATE PROCEDURE sp_CreateDefaultCategories
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO categories (user_id, name, type, icon)
    SELECT @UserId, v.name, v.type, v.icon
    FROM (VALUES
        (N'Продукты',            N'Расход', N'🛒'),
        (N'Транспорт',           N'Расход', N'🚌'),
        (N'Жильё и ЖКУ',         N'Расход', N'🏠'),
        (N'Связь и интернет',    N'Расход', N'📱'),
        (N'Кафе и рестораны',    N'Расход', N'🍽'),
        (N'Здоровье',            N'Расход', N'💊'),
        (N'Развлечения',         N'Расход', N'🎬'),
        (N'Одежда',              N'Расход', N'👕'),
        (N'Зарплата',            N'Доход',  N'💼'),
        (N'Подработка',          N'Доход',  N'🧾'),
        (N'Проценты по вкладам', N'Доход',  N'🏦'),
        (N'Подарки',             N'Доход',  N'🎁')
    ) v (name, type, icon)
    WHERE NOT EXISTS (SELECT 1 FROM categories c
                      WHERE c.user_id = @UserId AND c.name = v.name AND c.type = v.type);
END
GO

-- Проведение регулярных платежей, срок которых наступил
CREATE PROCEDURE sp_ProcessRecurringPayments
    @UserId int,
    @Today  date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Today IS NULL SET @Today = CAST(GETDATE() AS date);

    DECLARE @processed int = 0, @rows int;

    BEGIN TRANSACTION;

    -- цикл нужен, если платёж пропущен несколько раз подряд
    WHILE 1 = 1
    BEGIN
        INSERT INTO transactions (account_id, category_id, amount, transaction_date, comment)
        SELECT r.account_id, r.category_id, r.amount, r.next_payment_date, N'Регулярный платёж'
        FROM recurring_payments r
        JOIN accounts a ON a.id = r.account_id
        WHERE a.user_id = @UserId AND r.next_payment_date <= @Today;

        SET @rows = @@ROWCOUNT;
        IF @rows = 0 BREAK;
        SET @processed += @rows;

        UPDATE r
        SET next_payment_date = CASE r.frequency
                WHEN N'Ежедневно'   THEN DATEADD(DAY,   1, r.next_payment_date)
                WHEN N'Еженедельно' THEN DATEADD(WEEK,  1, r.next_payment_date)
                WHEN N'Ежемесячно'  THEN DATEADD(MONTH, 1, r.next_payment_date)
                ELSE                     DATEADD(YEAR,  1, r.next_payment_date)
            END
        FROM recurring_payments r
        JOIN accounts a ON a.id = r.account_id
        WHERE a.user_id = @UserId AND r.next_payment_date <= @Today;
    END

    IF @processed > 0
        INSERT INTO notifications (user_id, message)
        VALUES (@UserId, CONCAT(N'Проведено регулярных платежей: ', @processed));

    COMMIT TRANSACTION;

    SELECT @processed AS processed;
END
GO

-- Отчёт 1. Доходы и расходы по категориям за период
CREATE PROCEDURE sp_ReportByCategory
    @UserId   int,
    @DateFrom date,
    @DateTo   date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT type          AS [Тип],
           category_name AS [Категория],
           currency_code AS [Валюта],
           COUNT(*)      AS [Операций],
           SUM(amount)   AS [Сумма],
           CAST(SUM(amount) * 100.0
                / SUM(SUM(amount)) OVER (PARTITION BY type, currency_code)
                AS decimal(5, 2)) AS [Доля %]
    FROM vw_UserTransactions
    WHERE user_id = @UserId
      AND transaction_date BETWEEN @DateFrom AND @DateTo
    GROUP BY type, category_name, currency_code
    ORDER BY [Тип], [Валюта], [Сумма] DESC;
END
GO

-- Отчёт 2. Доходы, расходы и сальдо по месяцам за период
CREATE PROCEDURE sp_ReportByMonth
    @UserId   int,
    @DateFrom date,
    @DateTo   date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CONVERT(char(7), transaction_date, 126) AS [Месяц],
           currency_code AS [Валюта],
           SUM(CASE WHEN type = N'Доход'  THEN amount ELSE 0 END) AS [Доходы],
           SUM(CASE WHEN type = N'Расход' THEN amount ELSE 0 END) AS [Расходы],
           SUM(CASE WHEN type = N'Доход'  THEN amount ELSE -amount END) AS [Сальдо]
    FROM vw_UserTransactions
    WHERE user_id = @UserId
      AND transaction_date BETWEEN @DateFrom AND @DateTo
    GROUP BY CONVERT(char(7), transaction_date, 126), currency_code
    ORDER BY [Месяц], [Валюта];
END
GO

-- Отчёт 3. Исполнение бюджетов, действующих в выбранном периоде
CREATE PROCEDURE sp_ReportBudgetExecution
    @UserId   int,
    @DateFrom date,
    @DateTo   date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT category_name AS [Категория],
           period        AS [Период],
           start_date    AS [Начало],
           end_date      AS [Окончание],
           limit_amount  AS [Лимит],
           spent         AS [Потрачено],
           remaining     AS [Остаток],
           percent_used  AS [Исполнение %]
    FROM vw_BudgetExecution
    WHERE user_id = @UserId
      AND start_date <= @DateTo
      AND end_date >= @DateFrom
    ORDER BY start_date, category_name;
END
GO

-- Отчёт 4. Остатки на счетах на текущий момент
CREATE PROCEDURE sp_ReportAccountBalances
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.name    AS [Счёт],
           cur.code  AS [Валюта],
           a.balance AS [Остаток]
    FROM accounts a
    JOIN currencies cur ON cur.id = a.currency_id
    WHERE a.user_id = @UserId
    ORDER BY cur.code, a.name;
END
GO

-- Удаление пользователя вместе со всеми его данными (для администратора)
CREATE PROCEDURE sp_DeleteUser
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM notifications WHERE user_id = @UserId;
    DELETE FROM goals         WHERE user_id = @UserId;
    DELETE FROM budgets       WHERE user_id = @UserId;
    DELETE r FROM recurring_payments r
        JOIN accounts a ON a.id = r.account_id WHERE a.user_id = @UserId;
    DELETE t FROM transactions t
        JOIN accounts a ON a.id = t.account_id WHERE a.user_id = @UserId;
    DELETE FROM categories    WHERE user_id = @UserId;
    DELETE FROM accounts      WHERE user_id = @UserId;
    DELETE FROM users         WHERE id = @UserId;

    COMMIT TRANSACTION;
END
GO

/* ==========================================================================
   6. РОЛИ БАЗЫ ДАННЫХ И ПРАВА ДОСТУПА
   ========================================================================== */

-- Роль клиентского приложения: работа с данными, без изменения справочника ролей
CREATE ROLE finance_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO finance_app;
GRANT EXECUTE ON SCHEMA::dbo TO finance_app;
DENY INSERT, UPDATE, DELETE ON dbo.roles TO finance_app;
GO

-- Роль аналитика: только чтение представлений и отчётов
CREATE ROLE finance_analyst;
GRANT SELECT ON dbo.vw_UserTransactions TO finance_analyst;
GRANT SELECT ON dbo.vw_BudgetExecution TO finance_analyst;
GRANT EXECUTE ON dbo.sp_ReportByCategory TO finance_analyst;
GRANT EXECUTE ON dbo.sp_ReportByMonth TO finance_analyst;
GRANT EXECUTE ON dbo.sp_ReportBudgetExecution TO finance_analyst;
GRANT EXECUTE ON dbo.sp_ReportAccountBalances TO finance_analyst;
GO

/* Пример подключения отдельного входа SQL Server к роли приложения
   (требуется смешанный режим проверки подлинности):
CREATE LOGIN finance_app_login WITH PASSWORD = N'СложныйПароль_2026!';
CREATE USER finance_app_user FOR LOGIN finance_app_login;
ALTER ROLE finance_app ADD MEMBER finance_app_user;
*/

/* ==========================================================================
   7. ТЕСТОВЫЕ ДАННЫЕ
   Пароли: admin@finance.ru / Admin123!, ivanov@mail.ru и smirnova@mail.ru / User123!
   Хранятся только хеши PBKDF2-SHA256 (100 000 итераций, случайная соль).
   ========================================================================== */

INSERT INTO roles (name) VALUES (N'Администратор'), (N'Пользователь');

INSERT INTO currencies (code, name) VALUES
    (N'RUB', N'Российский рубль'),
    (N'USD', N'Доллар США'),
    (N'EUR', N'Евро'),
    (N'CNY', N'Китайский юань');

INSERT INTO users (role_id, full_name, email, password_hash, created_at) VALUES
    (1, N'Администратор сервиса', N'admin@finance.ru',
     N'PBKDF2$100000$bK5gyyxD39/Xprl00Jjqig==$Zul+qADp/Jxb3db1oca9hV28GHMb3rzfxi188MwrkOc=', '2026-09-01T10:00:00'),
    (2, N'Иванов Пётр Сергеевич', N'ivanov@mail.ru',
     N'PBKDF2$100000$5fAJcB12CVX9+a6uoCW7kg==$vOG8frfq7es0F0OSISVmMCDWumtDTfRy6EV02VMZ+Cc=', '2026-09-01T12:30:00'),
    (2, N'Смирнова Анна Олеговна', N'smirnova@mail.ru',
     N'PBKDF2$100000$IY+SM4GULhTcy3YLVLNLzA==$kZiKRsz8UPLk7zX7OdtFcfCc5eICbPVy2P6Qv7KaE2o=', '2026-10-01T09:15:00');
GO

EXEC sp_CreateDefaultCategories @UserId = 1;
EXEC sp_CreateDefaultCategories @UserId = 2;
EXEC sp_CreateDefaultCategories @UserId = 3;
GO

-- Начальные остатки счетов
INSERT INTO accounts (user_id, currency_id, name, balance) VALUES
    (2, 1, N'Основная карта',     25000.00),
    (2, 1, N'Наличные',            5000.00),
    (2, 1, N'Накопительный счёт', 100000.00),
    (2, 2, N'Долларовый счёт',      500.00),
    (3, 1, N'Зарплатная карта',   15000.00),
    (3, 1, N'Вклад',              50000.00);
GO

-- Бюджеты добавляются до операций, чтобы сработал триггер уведомлений
INSERT INTO budgets (user_id, category_id, limit_amount, period, start_date, end_date)
SELECT u.id, c.id, v.limit_amount, v.period, CAST(v.start_date AS date), CAST(v.end_date AS date)
FROM (VALUES
    (N'ivanov@mail.ru',   N'Развлечения',      3000.00, N'Месяц', '2026-10-01', '2026-10-31'),
    (N'ivanov@mail.ru',   N'Продукты',        15000.00, N'Месяц', '2026-11-01', '2026-11-30'),
    (N'ivanov@mail.ru',   N'Кафе и рестораны', 5000.00, N'Месяц', '2026-11-01', '2026-11-30'),
    (N'ivanov@mail.ru',   N'Транспорт',       10000.00, N'Квартал', '2026-10-01', '2026-12-31'),
    (N'smirnova@mail.ru', N'Продукты',         8000.00, N'Месяц', '2026-11-01', '2026-11-30')
) v (email, category, limit_amount, period, start_date, end_date)
JOIN users u      ON u.email = v.email
JOIN categories c ON c.user_id = u.id AND c.name = v.category AND c.type = N'Расход';
GO

INSERT INTO transactions (account_id, category_id, amount, transaction_date, comment)
SELECT a.id, c.id, v.amount, CAST(v.dt AS date), v.comment
FROM (VALUES
    (N'ivanov@mail.ru', N'Основная карта',     N'Зарплата',            85000.00, '2026-09-05', N'Зарплата за август'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Продукты',             4200.00, '2026-09-07', N'Гипермаркет'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Жильё и ЖКУ',         30000.00, '2026-09-10', N'Аренда квартиры'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Транспорт',            1500.00, '2026-09-12', N'Проездной'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Связь и интернет',      700.00, '2026-09-15', N'Домашний интернет'),
    (N'ivanov@mail.ru', N'Наличные',           N'Кафе и рестораны',     2300.00, '2026-09-18', N'Ужин с друзьями'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Продукты',             3800.00, '2026-09-20', NULL),
    (N'ivanov@mail.ru', N'Основная карта',     N'Развлечения',          1800.00, '2026-09-25', N'Кино'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Подработка',          12000.00, '2026-09-28', N'Разовый заказ'),
    (N'ivanov@mail.ru', N'Накопительный счёт', N'Проценты по вкладам',   650.00, '2026-09-30', NULL),
    (N'ivanov@mail.ru', N'Основная карта',     N'Зарплата',            85000.00, '2026-10-05', N'Зарплата за сентябрь'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Продукты',             5100.00, '2026-10-06', N'Гипермаркет'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Жильё и ЖКУ',         30000.00, '2026-10-10', N'Аренда квартиры'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Транспорт',            1700.00, '2026-10-11', N'Проездной'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Связь и интернет',      700.00, '2026-10-15', N'Домашний интернет'),
    (N'ivanov@mail.ru', N'Долларовый счёт',    N'Подработка',            200.00, '2026-10-15', N'Перевод от заказчика'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Здоровье',             2400.00, '2026-10-17', N'Аптека'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Кафе и рестораны',     3100.00, '2026-10-19', NULL),
    (N'ivanov@mail.ru', N'Основная карта',     N'Развлечения',          2200.00, '2026-10-21', N'Концерт'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Развлечения',          1500.00, '2026-10-26', N'Боулинг'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Одежда',               6500.00, '2026-10-28', N'Куртка'),
    (N'ivanov@mail.ru', N'Наличные',           N'Продукты',             4600.00, '2026-10-31', N'Рынок'),
    (N'ivanov@mail.ru', N'Накопительный счёт', N'Проценты по вкладам',   700.00, '2026-10-31', NULL),
    (N'ivanov@mail.ru', N'Основная карта',     N'Продукты',             2900.00, '2026-11-01', NULL),
    (N'ivanov@mail.ru', N'Основная карта',     N'Транспорт',             900.00, '2026-11-02', N'Такси'),
    (N'ivanov@mail.ru', N'Основная карта',     N'Кафе и рестораны',     1200.00, '2026-11-03', N'Обед'),
    (N'ivanov@mail.ru', N'Наличные',           N'Подарки',              5000.00, '2026-11-03', N'Подарок на день рождения'),
    (N'smirnova@mail.ru', N'Зарплатная карта', N'Зарплата',            62000.00, '2026-10-05', N'Зарплата за сентябрь'),
    (N'smirnova@mail.ru', N'Зарплатная карта', N'Продукты',             3500.00, '2026-10-08', NULL),
    (N'smirnova@mail.ru', N'Зарплатная карта', N'Транспорт',            1200.00, '2026-10-12', N'Метро'),
    (N'smirnova@mail.ru', N'Зарплатная карта', N'Кафе и рестораны',     1800.00, '2026-10-20', NULL),
    (N'smirnova@mail.ru', N'Вклад',            N'Проценты по вкладам',   400.00, '2026-10-31', NULL),
    (N'smirnova@mail.ru', N'Зарплатная карта', N'Продукты',             2100.00, '2026-11-01', NULL)
) v (email, account, category, amount, dt, comment)
JOIN users u      ON u.email = v.email
JOIN accounts a   ON a.user_id = u.id AND a.name = v.account
JOIN categories c ON c.user_id = u.id AND c.name = v.category;
GO

INSERT INTO goals (user_id, account_id, name, target_amount, current_amount, deadline)
SELECT u.id, a.id, v.name, v.target, v.current_amount, CAST(v.deadline AS date)
FROM (VALUES
    (N'ivanov@mail.ru',   N'Накопительный счёт', N'Отпуск на море',     150000.00, 60000.00, '2027-06-01'),
    (N'ivanov@mail.ru',   NULL,                  N'Новый ноутбук',       90000.00, 25000.00, '2027-03-01'),
    (N'smirnova@mail.ru', N'Вклад',              N'Курсы английского',   40000.00, 12000.00, '2027-01-31')
) v (email, account, name, target, current_amount, deadline)
JOIN users u         ON u.email = v.email
LEFT JOIN accounts a ON a.user_id = u.id AND a.name = v.account;
GO

INSERT INTO recurring_payments (account_id, category_id, amount, frequency, next_payment_date)
SELECT a.id, c.id, v.amount, v.frequency, CAST(v.next_date AS date)
FROM (VALUES
    (N'ivanov@mail.ru',   N'Основная карта',   N'Жильё и ЖКУ',      30000.00, N'Ежемесячно', '2026-11-10'),
    (N'ivanov@mail.ru',   N'Основная карта',   N'Связь и интернет',   700.00, N'Ежемесячно', '2026-11-15'),
    (N'ivanov@mail.ru',   N'Основная карта',   N'Зарплата',         85000.00, N'Ежемесячно', '2026-12-05'),
    (N'smirnova@mail.ru', N'Зарплатная карта', N'Связь и интернет',   500.00, N'Ежемесячно', '2026-11-03')
) v (email, account, category, amount, frequency, next_date)
JOIN users u      ON u.email = v.email
JOIN accounts a   ON a.user_id = u.id AND a.name = v.account
JOIN categories c ON c.user_id = u.id AND c.name = v.category;
GO

INSERT INTO notifications (user_id, message, is_read, created_at) VALUES
    (2, N'Добро пожаловать в сервис учёта личных финансов!', 1, '2026-09-01T12:31:00'),
    (3, N'Добро пожаловать в сервис учёта личных финансов!', 0, '2026-10-01T09:16:00');
GO
