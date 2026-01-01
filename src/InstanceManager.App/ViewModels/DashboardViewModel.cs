using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Navigation;
using System.Collections.ObjectModel;
using System.Text.Json;
using InstanceManager.App.Services;
using InstanceManager.Core.Errors;

namespace InstanceManager.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase, INavigationAware, ILoadOnceViewModel
{
    private readonly IAuthService _auth;
    private readonly INavigationService _nav;
    private readonly AppRootViewModel _root;
    private readonly LoginViewModel _login;

    private bool _loaded;

    [ObservableProperty] private string _status = "Not loaded.";
    [ObservableProperty] private bool _isBusy;

    public bool CanInteract => !IsBusy;
    public sealed record ProfileField(string Label, string Value);
    public ObservableCollection<ProfileField> Fields { get; } = new();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    public DashboardViewModel(
        INavigationService nav,
        IAuthService auth,
        AppRootViewModel root,
        LoginViewModel login,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services)
        : base(auth, root, session, exceptions, services)
    {
        _auth = auth;
        _nav = nav;
        _root = root;
        _login = login;
    }

    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        // auto-load when navigated to
        return EnsureLoadedAsync();
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        await LoadMeAsync();
    }

    [RelayCommand]
    private async Task LoadMeAsync()
    {
        IsBusy = true;
        try
        {
            var me = await _auth.GetCurrentUserAsync(CancellationToken.None);
            Console.WriteLine($"[Dashboard] Session.IsLoggedIn={Session.IsLoggedIn}, DisplayName={Session.DisplayName}");
            Console.WriteLine($"[Dashboard] CurrentUserRawJson length={Session.CurrentUserRawJson?.Length ?? 0}");
            if (!string.IsNullOrWhiteSpace(Session.CurrentUserRawJson))
                Console.WriteLine($"[Dashboard] CurrentUserRawJson={Session.CurrentUserRawJson}");

            Status = me is null
                ? "No user loaded."
                : $"Logged in as {me.DisplayName}";

            // DEBUG: Check if the JSON is available
            System.Diagnostics.Debug.WriteLine($"CurrentUserRawJson is null: {string.IsNullOrWhiteSpace(Session.CurrentUserRawJson)}");
            System.Diagnostics.Debug.WriteLine($"CurrentUserRawJson length: {Session.CurrentUserRawJson?.Length ?? 0}");

            if (!string.IsNullOrWhiteSpace(Session.CurrentUserRawJson))
            {
                Console.WriteLine("[Dashboard] Parsing CurrentUserRawJson");
                LoadFieldsFromRaw(Session.CurrentUserRawJson);
                Console.WriteLine($"[Dashboard] Fields count after parse: {Fields.Count}");
            }
            else
                Status = "User data not available in session.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] LoadMeAsync exception: {ex}");
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadFieldsFromRaw(string raw)
    {
        try
        {
            Fields.Clear();
            using var doc = JsonDocument.Parse(raw);
            Flatten("", doc.RootElement);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] LoadFieldsFromRaw error: {ex}");
            throw;
        }
    }

    private void Flatten(string prefix, JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? p.Name : $"{prefix}.{p.Name}";
                    if (key.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Flatten(key, p.Value);
                }
                break;

            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in el.EnumerateArray())
                {
                    var key = $"{prefix}[{i++}]";
                    if (key.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Flatten(key, item);
                }
                break;

            default:
                var v = el.ToString();
                if (!string.IsNullOrWhiteSpace(v) && v.StartsWith("usr_", StringComparison.OrdinalIgnoreCase)) return;
                Fields.Add(new ProfileField(prefix, v));
                break;
        }
    }
}
