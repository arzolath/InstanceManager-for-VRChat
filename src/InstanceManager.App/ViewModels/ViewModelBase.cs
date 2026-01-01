using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.App.Services;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace InstanceManager.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly IAuthService Auth;
    protected readonly AppRootViewModel Root;
    protected readonly ISessionState Session;
    protected readonly IExceptionReporter Exceptions;
    protected readonly IServiceProvider Services;

    protected ViewModelBase(
        IAuthService auth,
        AppRootViewModel root,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services)
    {
        Auth = auth;
        Root = root;
        Session = session;
        Exceptions = exceptions;
        Services = services;
    }

    public static string? AvatarUrlFromRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var r = doc.RootElement;

            static string? GetString(JsonElement e)
            {
                if (e.ValueKind != JsonValueKind.String) return null;
                var s = e.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }

            if (r.TryGetProperty("profilePicOverride", out var a))
                return GetString(a) ?? TryNext(r);

            return TryNext(r);

            static string? TryNext(JsonElement r)
            {
                if (r.TryGetProperty("userIcon", out var ui))
                {
                    var s = GetString(ui);
                    if (s is not null) return s;
                }

                if (r.TryGetProperty("currentAvatarThumbnailImageUrl", out var t))
                {
                    var s = GetString(t);
                    if (s is not null) return s;
                }

                if (r.TryGetProperty("currentAvatarImageUrl", out var i))
                {
                    var s = GetString(i);
                    if (s is not null) return s;
                }

                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        try
        {
            await Auth.LogoutAsync(CancellationToken.None);
            Session.Clear();
            Root.Current = Services.GetRequiredService<LoginViewModel>();
        }
        catch (Exception ex)
        {
            Exceptions.Report(ex, "Logout");
        }
    }

    [RelayCommand]
    public void OpenGithub()
    {
        try
        {
            const string url = "https://github.com/arzolath/InstanceManager-for-VRChat";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Exceptions.Report(ex, "Open GitHub");
        }
    }
}
