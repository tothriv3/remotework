# Личный бюджет: курсовой проект по ПМ.11

Тема: «Проектирование и разработка базы данных для сервиса по управлению личными финансами и бюджетом».

## Состав

| Путь | Что внутри |
|---|---|
| `database/PersonalFinanceDB.sql` | Скрипт создания БД: 10 таблиц, ограничения, индексы, триггеры, представления, процедуры, роли, тестовые данные |
| `database/LoadTest.sql` | Нагрузочный тест на 100 000 операций |
| `src/PersonalFinance.sln` | Решение Visual Studio |
| `src/PersonalFinance/` | WPF-приложение (.NET 8, EF Core 8, ADO.NET) |
| `tests/PersonalFinance.DataTests/` | Консольная проверка слоя данных на реальной БД (42 проверки) |
| `docs/Пояснительная_записка.docx` | Пояснительная записка по ГОСТ |
| `docs/Презентация.pptx` | Презентация к защите (с текстом доклада в заметках) |
| `docs/Отзыв_руководителя_бланк.docx` | Бланк отзыва для руководителя |
| `docs/diagrams/` | Диаграммы (PNG) и их исходники (PlantUML, Graphviz) |

## Запуск

1. Установить SQL Server Express и SQL Server Management Studio.
2. Открыть в SSMS `database/PersonalFinanceDB.sql` и выполнить (F5).
3. Открыть `src/PersonalFinance.sln` в Visual Studio 2022 (нужна рабочая нагрузка «Разработка классических приложений .NET»).
4. Если экземпляр SQL Server называется не `.\SQLEXPRESS`, поменять `Server=` в `src/PersonalFinance/App.config`.
5. Запустить проект PersonalFinance (F5).

Тестовые учётные записи:

| Email | Пароль | Роль |
|---|---|---|
| admin@finance.ru | Admin123! | Администратор |
| ivanov@mail.ru | User123! | Пользователь |
| smirnova@mail.ru | User123! | Пользователь |

Проверка слоя данных: пересоздать БД скриптом и запустить проект `PersonalFinance.DataTests`.
