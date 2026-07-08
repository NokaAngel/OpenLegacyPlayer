using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Threading;

namespace OpenLegacyPlayer;

public partial class App : Application
{
    // Stable identifiers so only one OpenLegacy Player runs and later launches
    // (e.g. double-clicking a media file) forward their arguments to it.
    private const string MutexName = "OpenLegacyPlayer.SingleInstance.9F1A6B2C";
    private const string PipeName = "OpenLegacyPlayer.Pipe.9F1A6B2C";

    private Mutex? _mutex;
    private MainWindow? _mainWindow;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.ExceptionObject as Exception);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Another copy is already running — hand it our files and bow out.
            ForwardToRunningInstance(e.Args);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Show();

        StartPipeServer();

        if (e.Args.Length > 0)
            _mainWindow.OpenFiles(e.Args);
    }

    // ── Single-instance IPC ────────────────────────────────────────────────

    private void StartPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();

                    using var reader = new StreamReader(server);
                    var files = new List<string>();
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                        if (line.Length > 0) files.Add(line);

                    if (files.Count > 0)
                        Dispatcher.Invoke(() =>
                        {
                            _mainWindow?.OpenFiles(files.ToArray());
                            _mainWindow?.BringToFront();
                        });
                }
                catch
                {
                    // A malformed connection shouldn't kill the listener.
                }
            }
        });
    }

    private static void ForwardToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            foreach (var arg in args)
                writer.WriteLine(arg);
        }
        catch
        {
            // The other instance may be starting up or exiting; nothing to do.
        }
    }

    // ── Diagnostics ────────────────────────────────────────────────────────

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => Log(e.Exception);

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
