using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InstanceManager.App.Views;

public partial class OwnedInstancesView : UserControl
{
    public OwnedInstancesView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
