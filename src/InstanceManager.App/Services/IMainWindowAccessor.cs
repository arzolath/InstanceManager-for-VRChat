using Avalonia.Controls;

namespace InstanceManager.App.Services;

public interface IMainWindowAccessor
{
    Window? MainWindow { get; set; }
}
