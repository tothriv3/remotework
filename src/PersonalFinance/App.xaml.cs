using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace PersonalFinance;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Русские форматы дат и чисел во всех элементах интерфейса
        CultureInfo.DefaultThreadCurrentCulture = Ui.Ru;
        CultureInfo.DefaultThreadCurrentUICulture = Ui.Ru;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(Ui.Ru.IetfLanguageTag)));

        // Любая непредвиденная ошибка показывается сообщением, а не закрывает программу
        DispatcherUnhandledException += (_, args) =>
        {
            Ui.Error(args.Exception);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
