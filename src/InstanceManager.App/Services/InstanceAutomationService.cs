using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Blocks;
using InstanceManager.Core.Errors;
using InstanceManager.Core.RateLimiting;
using InstanceManager.Storage.Logs;
using InstanceManager.VRChat;
using InstanceManager.VRChat.Instances;
using Microsoft.Extensions.DependencyInjection;
using VRChat.API.Api;
using VRChat.API.Model;

namespace InstanceManager.App.Services;

public sealed class InstanceAutomationService : IDisposable
{
    private readonly ISessionState _session;
    private readonly IAuthService _auth;
    private readonly IKickLogStore _log;
    private readonly IVrchatApiContext _ctx;
    private readonly IServiceProvider _services;
    private readonly IExceptionReporter _exceptions;
    private readonly IApiRateLimiter _rateLimiter;
    private readonly TimeSpan _interval;

    private CancellationTokenSource? _cts;
    private Task? _runner;
    private PeriodicTimer? _timer;
    private readonly object _sync = new();

    public InstanceAutomationService(
        ISessionState session,
        IAuthService auth,
        IKickLogStore log,
        IVrchatApiContext ctx,
        IServiceProvider services,
        IApiRateLimiter rateLimiter,
        IExceptionReporter exceptions)
    {
        _session = session;
        _auth = auth;
        _log = log;
        _ctx = ctx;
        _services = services;
        _exceptions = exceptions;
        _rateLimiter = rateLimiter;
        _interval = TimeSpan.FromMinutes(1);

        _session.Changed += OnSessionChanged;

        if (_session.IsLoggedIn)
            Start();
    }

    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        _ = StopAsync();
    }

    private void OnSessionChanged()
    {
        if (_session.IsLoggedIn)
            Start();
        else
            _ = StopAsync();
    }

    private void Start()
    {
        lock (_sync)
        {
            if (_runner is not null) return;

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(_interval);
            _runner = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    private async Task StopAsync()
    {
        Task? runner;

        lock (_sync)
        {
            runner = _runner;
            _runner = null;
            _cts?.Cancel();
            _timer?.Dispose();
        }

        if (runner is null) return;

        try
        {
            await runner.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore expected cancellation on shutdown
        }
        finally
        {
            lock (_sync)
            {
                _cts?.Dispose();
                _cts = null;
                _timer = null;
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var timer = _timer ?? throw new InvalidOperationException("Timer missing.");

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await ScanAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        catch (ObjectDisposedException)
        {
            // stopping
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        if (!_ctx.IsReady)
            return;

        var me = await _auth.GetCurrentUserAsync(ct).ConfigureAwait(false);
        if (me is null)
            return;

        var blocks = _services.GetRequiredService<IBlockListService>();

        IReadOnlyCollection<string> blockedIds;
        try
        {
            blockedIds = await blocks.GetEffectiveBlockedUserIdsAsync(me.UserId, BlockSourceMode.Both, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptions.Report(ex, "Automation: load block list");
            return;
        }

        if (blockedIds.Count == 0)
            return;

        var blockedSet = new HashSet<string>(blockedIds, StringComparer.Ordinal);

        var instances = _services.GetRequiredService<VrchatInstanceApi>();
        var moderation = _services.GetRequiredService<PlayermoderationApi>();

        IReadOnlyCollection<string> locations;
        try
        {
            locations = await instances.GetRecentLocationsAsync(limit: 50, offset: 0, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptions.Report(ex, "Automation: list locations");
            return;
        }

        foreach (var location in locations)
        {
            if (!TrySplitLocation(location, out var worldId, out var instanceId))
                continue;

            Instance? instance;
            try
            {
                instance = await instances.GetInstanceAsync(worldId, instanceId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _exceptions.Report(ex, $"Automation: fetch instance {location}");
                continue;
            }

            if (instance is null || !instance.Active)
                continue;

            if (!IsOwnedBy(instance, me.UserId))
                continue;

            if (instance.Users is null || instance.Users.Count == 0)
                continue;

            foreach (var user in instance.Users)
            {
                if (user?.Id is null) continue;
                if (!blockedSet.Contains(user.Id)) continue;

                await HandleBlockedUserAsync(moderation, instance, user, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleBlockedUserAsync(PlayermoderationApi moderation, Instance instance, LimitedUserInstance user, CancellationToken ct)
    {
        var instanceName = instance.DisplayName ?? instance.Name ?? instance.Location;
        var instanceIdentifier = instance.InstanceId ?? instance.Id ?? instance.Location ?? string.Empty;

        try
        {
            await _rateLimiter.RunAsync(
                _ => Task.Run(() => moderation.ModerateUser(new ModerateUserRequest(user.Id, PlayerModerationType.Block)), ct),
                ct
            ).ConfigureAwait(false);

            await _log.AppendAsync(
                new KickLogEntry(
                    DateTimeOffset.UtcNow,
                    instance.WorldId ?? string.Empty,
                    instanceIdentifier,
                    instanceName,
                    user.Id,
                    user.DisplayName,
                    "kicked",
                    "Blocked user found in owned instance"),
                ct
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptions.Report(ex, $"Automation: moderate {user.Id}");

            await _log.AppendAsync(
                new KickLogEntry(
                    DateTimeOffset.UtcNow,
                    instance.WorldId ?? string.Empty,
                    instanceIdentifier,
                    instanceName,
                    user.Id,
                    user.DisplayName,
                    "failed",
                    ex.Message),
                ct
            ).ConfigureAwait(false);
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
