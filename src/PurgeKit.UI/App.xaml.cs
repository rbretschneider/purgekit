using System.Windows;
using System.Windows.Threading;
using PurgeKit.Core.Models;
using PurgeKit.Core.Services;

namespace PurgeKit.UI;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Settings = AppSettings.Load();

        if (!ElevationHelper.IsRunningAsAdmin())
        {
            var result = MessageBox.Show(
                "PurgeKit needs administrator access to scan system folders.\n\nClick OK to re-launch with elevated permissions.",
                "PurgeKit",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.OK && ElevationHelper.RelaunchAsAdmin())
            {
                Shutdown();
                return;
            }
        }

        ApplyTheme(Settings.Theme);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.LogError("UI Thread", e.Exception);
        ShowErrorDialog(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogService.LogError("AppDomain", ex);
            ShowErrorDialog(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogService.LogError("Task", e.Exception);
        e.SetObserved();
    }

    private static void ShowErrorDialog(Exception ex)
    {
        var details = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        var msg = $"An unexpected error occurred.\n\n{ex.Message}\n\nWould you like to copy the error details to clipboard?";

        var result = MessageBox.Show(msg, "PurgeKit — Error",
            MessageBoxButton.YesNo, MessageBoxImage.Error);

        if (result == MessageBoxResult.Yes)
        {
            try { Clipboard.SetText(details); } catch { }
        }
    }

    public static void ApplyTheme(string theme)
    {
        var requested = theme switch
        {
            "Light" => ModernWpf.ApplicationTheme.Light,
            "Dark" => ModernWpf.ApplicationTheme.Dark,
            _ => (ModernWpf.ApplicationTheme?)null
        };

        if (requested.HasValue)
            ModernWpf.ThemeManager.Current.ApplicationTheme = requested.Value;
        else
            ModernWpf.ThemeManager.Current.ApplicationTheme = null;
    }
}
