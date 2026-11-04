using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using PersonalFinance.Data;

namespace PersonalFinance.Views;

public partial class ReportsPage : UserControl
{
    // Отчёт = хранимая процедура; UsesPeriod — нужны ли даты начала и окончания
    private record Report(string Title, string Procedure, bool UsesPeriod);

    private static readonly Report[] Reports =
    {
        new("Доходы и расходы по категориям за период", "sp_ReportByCategory", true),
        new("Доходы, расходы и сальдо по месяцам", "sp_ReportByMonth", true),
        new("Исполнение бюджетов за период", "sp_ReportBudgetExecution", true),
        new("Остатки на счетах на текущий момент", "sp_ReportAccountBalances", false)
    };

    public ReportsPage()
    {
        InitializeComponent();
        ReportBox.ItemsSource = Reports;
        ReportBox.SelectedIndex = 0;
        FromBox.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
        ToBox.SelectedDate = DateTime.Today;
    }

    private void ReportBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool usesPeriod = ((Report)ReportBox.SelectedItem).UsesPeriod;
        FromBox.IsEnabled = usesPeriod;
        ToBox.IsEnabled = usesPeriod;
    }

    // Формирование отчёта через ADO.NET: SqlConnection + SqlCommand + DataTable
    private void Build_Click(object sender, RoutedEventArgs e)
    {
        var report = (Report)ReportBox.SelectedItem;
        var parameters = new List<SqlParameter> { new("@UserId", Session.UserId) };

        if (report.UsesPeriod)
        {
            if (FromBox.SelectedDate is not DateTime from || ToBox.SelectedDate is not DateTime to)
            { Ui.Warning("Укажите период отчёта."); return; }
            if (to < from) { Ui.Warning("Дата окончания не может быть раньше даты начала."); return; }
            parameters.Add(new SqlParameter("@DateFrom", from));
            parameters.Add(new SqlParameter("@DateTo", to));
        }

        try
        {
            var table = Db.ExecuteProcedure(report.Procedure, parameters.ToArray());
            Grid.ItemsSource = table.DefaultView;
            InfoText.Text = table.Rows.Count == 0 ? "Нет данных за выбранный период" : $"Строк: {table.Rows.Count}";
        }
        catch (Exception ex)
        {
            Ui.Error(ex);
        }
    }

    // Формат дат и денежных сумм в автоматически созданных столбцах
    private void Grid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is not DataGridTextColumn column)
            return;
        if (e.PropertyType == typeof(DateTime))
            column.Binding.StringFormat = "dd.MM.yyyy";
        else if (e.PropertyType == typeof(decimal))
            column.Binding.StringFormat = "N2";
    }
}
