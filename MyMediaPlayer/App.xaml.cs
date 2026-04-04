using System;
using System.Windows;
using System.Windows.Threading;
using MyMediaPlayer.Services;

namespace MyMediaPlayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LoggingService.Instance.LogError("Unhandled UI exception", e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LoggingService.Instance.LogError("Unhandled domain exception", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggingService.Instance.LogInfo("Application exit");
        base.OnExit(e);
    }
}
