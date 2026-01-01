using System;

namespace InstanceManager.App.Services;

public sealed class SessionState : ISessionState
{
    public bool IsLoggedIn { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string? AvatarUrl { get; private set; }
    public string? CurrentUserRawJson { get; private set; }

    public event Action? Changed;

    public void SetLoggedIn(string displayName, string? avatarUrl, string? currentUserRawJson)
    {
        IsLoggedIn = true;
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        CurrentUserRawJson = currentUserRawJson;
        Changed?.Invoke();
    }

    public void Clear()
    {
        IsLoggedIn = false;
        DisplayName = "";
        AvatarUrl = null;
        CurrentUserRawJson = null;
        Changed?.Invoke();
    }
}
