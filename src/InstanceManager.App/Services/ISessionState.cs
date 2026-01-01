using System;

namespace InstanceManager.App.Services;

public interface ISessionState
{
    bool IsLoggedIn { get; }
    string DisplayName { get; }
    string? AvatarUrl { get; }
    string? CurrentUserRawJson { get; }

    event Action? Changed;

    void SetLoggedIn(string displayName, string? avatarUrl, string? currentUserRawJson);
    void Clear();
}
