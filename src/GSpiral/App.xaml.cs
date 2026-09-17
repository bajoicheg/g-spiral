using System.Windows;
using System.Windows.Threading;

namespace GSpiral;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            var smokeWindow = new MainWindow();
            smokeWindow.Show();
            Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            smokeWindow.Close();
            Shutdown(0);
            return;
        }

        var window = new MainWindow();
        window.Closed += (_, _) => Shutdown(0);
        MainWindow = window;
        window.Show();
    }
}
