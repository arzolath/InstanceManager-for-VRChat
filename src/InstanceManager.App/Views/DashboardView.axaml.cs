using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using InstanceManager.App.ViewModels;

namespace InstanceManager.App.Views;

public partial class DashboardView : UserControl
{
    private bool _isInitialized;

    public DashboardView()
    {
        AvaloniaXamlLoader.Load(this);
        this.AttachedToVisualTree += async (s, e) =>
        {
            if (!_isInitialized && this.DataContext is DashboardViewModel vm && vm.LoadMeCommand.CanExecute(null))
            {
                _isInitialized = true;
                await vm.LoadMeCommand.ExecuteAsync(null);
            }
        };
    }
}
