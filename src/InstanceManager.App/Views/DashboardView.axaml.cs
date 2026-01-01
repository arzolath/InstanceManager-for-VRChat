using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InstanceManager.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
