using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace VLIT;

public partial class App : Application
{
    private static int _isHandlingFatalException;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
        if (Interlocked.Exchange(ref _isHandlingFatalException, 1) == 0)
        {
            try
            {
                MessageBox.Show(
                    $"VLIT hit an unexpected error and wrote details to:{Environment.NewLine}{CrashLogPath}",
                    "VLIT Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Current.Shutdown(1);
            }
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            WriteCrashLog("UnhandledException", exception);
        }
        else
        {
            WriteCrashLog("UnhandledException", new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown fatal error"));
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VLIT",
        "crash.log");

    private static void WriteCrashLog(string source, Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                CrashLogPath,
                $"""

                [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}
                {exception}
                """);
        }
        catch
        {
            // Last-chance crash logging must never throw.
        }
    }
}
