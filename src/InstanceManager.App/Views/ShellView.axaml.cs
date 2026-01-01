using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InstanceManager.App.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
