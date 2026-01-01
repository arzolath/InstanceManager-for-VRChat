using System;
using Avalonia.Threading;
using InstanceManager.App.ViewModels;
using InstanceManager.App.Views;
using InstanceManager.Core.Errors;

namespace InstanceManager.App.Services;

public sealed class AvaloniaExceptionReporter : IExceptionReporter
{
    private readonly IMainWindowAccessor _window;

    public AvaloniaExceptionReporter(IMainWindowAccessor window)
    {
        _window = window;
    }

    public void Report(Exception ex, string context)
    {
        var msg = $"{context}\n\n{ex}";
        Dispatcher.UIThread.Post(async () =>
        {
            var dlg = new ErrorDialog
            {
                DataContext = new ErrorDialogViewModel("Something went wrong", msg)
            };

            if (_window.MainWindow is not null)
                await dlg.ShowDialog(_window.MainWindow);
            else
                dlg.Show();
        });
    }
}
