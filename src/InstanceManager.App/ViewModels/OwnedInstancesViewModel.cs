using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.App.Services;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Errors;
using InstanceManager.Core.Navigation;
using InstanceManager.Storage.Logs;
using InstanceManager.VRChat;
using InstanceManager.VRChat.Instances;
using VRChat.API.Model;
using VRChat.API.Api;

namespace InstanceManager.App.ViewModels;

public sealed partial class OwnedInstancesViewModel : ViewModelBase, INavigationAware, ILoadOnceViewModel
{
    private readonly VrchatInstanceApi _instances;
    private readonly IKickLogStore _log;
    private readonly INavigationService _nav;
    private readonly IVrchatApiContext _ctx;

    private bool _loaded;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Not loaded.";
    public bool CanInteract => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    public ObservableCollection<OwnedInstanceItem> Instances { get; } = new();
    public ObservableCollection<KickLogEntry> KickEvents { get; } = new();

    public OwnedInstancesViewModel(
        INavigationService nav,
        VrchatInstanceApi instances,
        IKickLogStore log,
        IVrchatApiContext ctx,
        IAuthService auth,
        AppRootViewModel root,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services)
        : base(auth, root, session, exceptions, services)
    {
        _nav = nav;
        _instances = instances;
        _log = log;
        _ctx = ctx;
    }

    public Task OnNavigatedToAsync(CancellationToken ct) => EnsureLoadedAsync();

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_ctx.IsReady)
        {
            Status = "Not logged in.";
            return;
        }

        IsBusy = true;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var ct = cts.Token;

            var me = await Auth.GetCurrentUserAsync(ct).ConfigureAwait(false);
            if (me is null)
            {
                Status = "User not loaded.";
                return;
            }

            var owned = new List<OwnedInstanceItem>();

            IReadOnlyCollection<string> recent;
            try
            {
                recent = await _instances.GetRecentLocationsAsync(50, 0, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Exceptions.Report(ex, "List recent locations");
                Status = ex.Message;
                return;
            }

            foreach (var loc in recent)
            {
                if (!TrySplitLocation(loc, out var worldId, out var instanceId))
                    continue;

                Instance? instance;
                try
                {
                    instance = await _instances.GetInstanceAsync(worldId, instanceId, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Exceptions.Report(ex, $"Get instance {loc}");
                    continue;
                }

                if (instance is null || !instance.Active)
                    continue;

                if (!IsOwnedBy(instance, me.UserId))
                    continue;

                var users = instance.Users?
                    .Where(u => u?.Id is not null)
                    .Select(u => new InstanceUser(u!.Id!, u.DisplayName))
                    .ToArray() ?? Array.Empty<InstanceUser>();

                owned.Add(new OwnedInstanceItem(
                    Name: instance.DisplayName ?? instance.Name ?? instance.Location ?? loc,
                    Location: instance.Location ?? $"{worldId}:{instanceId}",
                    WorldId: worldId,
                    InstanceId: instanceId,
                    UserCount: instance.UserCount,
                    Capacity: instance.Capacity,
                    Users: users
                ));
            }

            Instances.Clear();
            foreach (var item in owned.OrderByDescending(o => o.UserCount))
                Instances.Add(item);

            var kicks = await _log.LoadAsync(ct).ConfigureAwait(false);
            KickEvents.Clear();
            foreach (var evt in kicks.OrderByDescending(k => k.Timestamp))
                KickEvents.Add(evt);

            Status = owned.Count == 0 ? "No owned instances found." : $"Showing {owned.Count} owned instance(s).";
        }
        catch (Exception ex)
        {
            Exceptions.Report(ex, "Refresh owned instances");
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool TrySplitLocation(string location, out string worldId, out string instanceId)
    {
        worldId = "";
        instanceId = "";

        if (string.IsNullOrWhiteSpace(location))
            return false;

        var idx = location.IndexOf(':');
        if (idx <= 0 || idx >= location.Length - 1)
            return false;

        worldId = location[..idx];
        instanceId = location[(idx + 1)..];
        return !string.IsNullOrWhiteSpace(worldId) && !string.IsNullOrWhiteSpace(instanceId);
    }

    private static bool IsOwnedBy(Instance instance, string userId)
    {
        if (string.Equals(instance.OwnerId, userId, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(instance.Hidden) && string.Equals(instance.Hidden, userId, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(instance.Friends) && string.Equals(instance.Friends, userId, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(instance.Private) && string.Equals(instance.Private, userId, StringComparison.Ordinal))
            return true;

        return false;
    }
}

public sealed record OwnedInstanceItem(
    string Name,
    string Location,
    string WorldId,
    string InstanceId,
    int? UserCount,
    int? Capacity,
    IReadOnlyCollection<InstanceUser> Users);

public sealed record InstanceUser(string UserId, string? DisplayName)
{
    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? UserId : DisplayName;
}
