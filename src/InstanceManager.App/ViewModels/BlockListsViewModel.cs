using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.App.Services;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Blocks;
using InstanceManager.Core.Errors;
using InstanceManager.Core.Navigation;


namespace InstanceManager.App.ViewModels;

public partial class BlockListsViewModel : ViewModelBase, INavigationAware, ILoadOnceViewModel
{
    private readonly IAuthService _auth;
    private readonly IBlockListService _blocks;

    private bool _loaded;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public sealed record BlockedUserRow(string DisplayName, string UserId);
    public ObservableCollection<BlockedUserRow> VrchatBlockedUsers { get; } = new();
    public int BlockedCount => VrchatBlockedUsers.Count;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanInteract => !IsBusy;

    public BlockListsViewModel(
        INavigationService nav,
        IAuthService auth,
        AppRootViewModel root,
        LoginViewModel login,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services,
        IBlockListService blocks)
        : base(auth, root, session, exceptions, services)
    {
        _auth = auth;
        _blocks = blocks;
        VrchatBlockedUsers.CollectionChanged += (_, __) => OnPropertyChanged(nameof(BlockedCount));
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        return EnsureLoadedAsync();
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        await RefreshVrchatBlocksAsync();
    }

    [RelayCommand]
    public async Task RefreshVrchatBlocksAsync()
    {
        Error = null;
        IsBusy = true;

        try
        {
            var me = await _auth.GetCurrentUserAsync(CancellationToken.None);
            if (me is null)
            {
                Error = "Not logged in. Go back to Login.";
                return;
            }

            var users = await _blocks.GetVrchatBlockedUsersAsync(CancellationToken.None, useCache: true);

            VrchatBlockedUsers.Clear();
            foreach (var u in users)
                VrchatBlockedUsers.Add(new BlockedUserRow(u.DisplayName ?? "(unknown)", u.UserId));
            OnPropertyChanged(nameof(BlockedCount));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
