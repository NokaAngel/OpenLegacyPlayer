using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace OpenLegacyPlayer;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.ExceptionObject as Exception);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
    }

    private static void Log(Exception? ex)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "startup-error.log"),
                ex?.ToString() ?? "unknown");
        }
        catch { }
    }
}
