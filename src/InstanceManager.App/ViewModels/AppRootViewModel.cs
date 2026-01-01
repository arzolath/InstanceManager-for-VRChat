using CommunityToolkit.Mvvm.ComponentModel;

namespace InstanceManager.App.ViewModels;

public partial class AppRootViewModel : ObservableObject
{
    [ObservableProperty]
    private object _current = null!;
}
