using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.Core.Navigation;
using InstanceManager.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Errors;
using System;

namespace InstanceManager.App.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    public INavigationService Nav { get; }

    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string? _avatarUrl;

    private readonly ISessionState _session;

    public ShellViewModel(
        INavigationService nav,
        IAuthService auth,
        AppRootViewModel root,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services)
        : base(auth, root, session, exceptions, services)
    {
        Nav = nav;

        _session = session;
        SyncFromSession();
        _session.Changed += SyncFromSession;
    }


    [RelayCommand]
    public async Task GoDashboardAsync()
    {
        // Don’t allow navigation when logged out
        if (!_session.IsLoggedIn)
        {
            Exceptions.Report(new InvalidOperationException("Please login first."), "Navigation");
            return;
        }

        try
        {
            Nav.NavigateTo<DashboardViewModel>();

            if (Nav.CurrentViewModel is ILoadOnceViewModel loadOnce)
                await loadOnce.EnsureLoadedAsync();
        }
        catch (Exception ex)
        {
            Exceptions.Report(ex, "Navigate to Profile");
        }
    }

    [RelayCommand]
    public async Task GoBlockListsAsync()
    {
        if (!_session.IsLoggedIn)
        {
            Exceptions.Report(new InvalidOperationException("Please login first."), "Navigation");
            return;
        }

        try
        {
            Nav.NavigateTo<BlockListsViewModel>();

            if (Nav.CurrentViewModel is ILoadOnceViewModel loadOnce)
                await loadOnce.EnsureLoadedAsync();
        }
        catch (Exception ex)
        {
            Exceptions.Report(ex, "Navigate to Block Lists");
        }
    }

    private void SyncFromSession()
    {
        IsLoggedIn = _session.IsLoggedIn;
        DisplayName = _session.DisplayName;
        AvatarUrl = _session.AvatarUrl;
    }
}
