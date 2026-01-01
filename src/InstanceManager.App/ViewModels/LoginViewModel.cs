using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Navigation;
using InstanceManager.App.ViewModels;
using InstanceManager.Core.Errors;
using InstanceManager.App.Services;
using InstanceManager.VRChat.Auth;
using System.Text.Json;

namespace InstanceManager.App.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly INavigationService _nav;
    private readonly AppRootViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IExceptionReporter _exceptions;
    private readonly ISessionState _session;

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private bool _isTwoFactorRequired;
    [ObservableProperty] private string _twoFactorCode = "";
    [ObservableProperty] private TwoFactorMethod _selectedTwoFactorMethod;
    public ObservableCollection<TwoFactorMethod> AvailableTwoFactorMethods { get; } = new();

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanInteract => !IsBusy;

    public LoginViewModel(
        ShellViewModel shell,
        INavigationService nav,
        IAuthService auth,
        AppRootViewModel root,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services)
        : base(auth, root, session, exceptions, services)
    {
        _auth = auth;
        _nav = nav;
        _root = root;
        _shell = shell;
        _exceptions = exceptions;
        _session = session;
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    [RelayCommand]
    private async Task LoginAsync()
    {
        Error = null;
        IsBusy = true;
        IsTwoFactorRequired = false;
        AvailableTwoFactorMethods.Clear();
        TwoFactorCode = "";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = await _auth.LoginAsync(Username, Password, cts.Token);

            if (result.Status == AuthStatus.Success)
            {
                var me = await _auth.GetCurrentUserAsync(CancellationToken.None);
                var raw = (_auth as VRChatAuthService)?.LastCurrentUserRawJson;
                var avatar = AvatarUrlFromRaw(raw);
                Console.WriteLine($"[Login] raw length={raw?.Length ?? 0}");
                if (!string.IsNullOrWhiteSpace(raw))
                    Console.WriteLine($"[Login] raw={raw}");

                _session.SetLoggedIn(me?.DisplayName ?? "Logged in", avatar, raw);

                _root.Current = _shell;
                _nav.NavigateTo<DashboardViewModel>();
                return;
            }

            if (result.Status == AuthStatus.RequiresTwoFactor && result.RequiredMethods is not null)
            {
                foreach (var m in result.RequiredMethods)
                    AvailableTwoFactorMethods.Add(m);

                SelectedTwoFactorMethod = AvailableTwoFactorMethods[0];
                IsTwoFactorRequired = true;

                Error = "Two-factor authentication required. Enter the code and press Verify.";
                return;
            }

            Error = result.ErrorMessage ?? "Login failed.";
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

    [RelayCommand]
    private async Task VerifyTwoFactorAsync()
    {
        Error = null;
        IsBusy = true;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = await _auth.SubmitTwoFactorAsync(SelectedTwoFactorMethod, TwoFactorCode, cts.Token);

            if (result.Status == AuthStatus.Success)
            {
                var me = await _auth.GetCurrentUserAsync(CancellationToken.None);
                var raw = (_auth as VRChatAuthService)?.LastCurrentUserRawJson;
                var avatar = AvatarUrlFromRaw(raw);
                Console.WriteLine($"[2FA] raw length={raw?.Length ?? 0}");
                if (!string.IsNullOrWhiteSpace(raw))
                    Console.WriteLine($"[2FA] raw={raw}");

                _session.SetLoggedIn(me?.DisplayName ?? "Logged in", avatar, raw);
                _root.Current = _shell;
                _nav.NavigateTo<DashboardViewModel>();
                return;
            }

            if (result.Status == AuthStatus.RequiresTwoFactor)
            {
                Error = "2FA still required. Check the code and try again.";
                return;
            }

            var msg = result.ErrorMessage ?? "2FA verification failed.";
            Error = msg;
            _exceptions.Report(new InvalidOperationException(msg), "Two-factor verification");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _exceptions.Report(ex, "Two-factor verification");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
