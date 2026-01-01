using Avalonia.Controls;

namespace InstanceManager.App.Services;

public sealed class MainWindowAccessor : IMainWindowAccessor
{
    public Window? MainWindow { get; set; }
}
