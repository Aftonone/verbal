using System.Windows;
using Application = System.Windows.Application;

namespace Verbal;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        if (e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
        {
            mainWindow.StartMinimizedToTray();
        }
        else
        {
            mainWindow.Show();
        }
    }
}
