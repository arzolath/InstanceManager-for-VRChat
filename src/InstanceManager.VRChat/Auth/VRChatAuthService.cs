using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.Auth;
using InstanceManager.Core.RateLimiting;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;
using CurrentUser = VRChat.API.Model.CurrentUser;
using System.Linq;
using InstanceManager.Storage.Auth;
using InstanceManager.VRChat;
using System.Text.Json;
using System.Net;

namespace InstanceManager.VRChat.Auth;

public sealed class VRChatAuthService : IAuthService, IVrchatApiContext
{
    private readonly IAuthRateLimiter _rateLimiter;

    private Configuration? _config;
    private ApiClient? _client;
    private AuthenticationApi? _authApi;

    private CurrentUser? _currentUser;
    private InstanceManager.Core.Auth.CurrentUser? _me;

    private readonly ICookieStore _cookieStore;

    public ApiClient Client => _client ?? throw new InvalidOperationException("VRChat client not ready.");
    public Configuration Config => _config ?? throw new InvalidOperationException("VRChat config not ready.");
    public bool IsReady => _client is not null && _config is not null;
    public string? LastCurrentUserRawJson { get; private set; }

    public VRChatAuthService(IAuthRateLimiter rateLimiter, ICookieStore cookieStore)
    {
        _rateLimiter = rateLimiter;
        _cookieStore = cookieStore;
    }

    public async Task<AuthResult> TryRestoreSessionAsync(CancellationToken ct)
    {
        _me = null;
        _currentUser = null;

        // Build config/client without username/password, cookie-only session
        _config = new Configuration
        {
            UserAgent = "InstanceManagerForVRChat/0.1.0 github"
        };

        _client = new ApiClient();
        _authApi = new AuthenticationApi(_client, _client, _config);

        // Apply previously saved cookies
        await TryApplySavedCookiesAsync(ct).ConfigureAwait(false);

        // If no cookie was loaded, bail fast
        if (!_config.DefaultHeaders.TryGetValue("Cookie", out var cookie) || string.IsNullOrWhiteSpace(cookie))
        {
            Console.WriteLine("[Auth] TryRestoreSession: no cookie to use.");
            return new AuthResult(AuthStatus.Failed, "No saved session.", null);
        }

        try
        {
            var resp = await _rateLimiter.RunAsync(
                _ => Task.Run(() => _authApi.GetCurrentUserWithHttpInfo(), ct),
                ct
            ).ConfigureAwait(false);
            CaptureLastCurrentUserRaw(resp);
            Console.WriteLine($"[Auth] TryRestoreSession: GetCurrentUser Data null? {resp.Data is null}, raw length={resp.RawContent?.Length ?? 0}");

            // If SDK gives typed Data, use it
            if (resp.Data is not null)
            {
                _currentUser = resp.Data;
                _me = new InstanceManager.Core.Auth.CurrentUser(_currentUser.Id, _currentUser.DisplayName);
                return new AuthResult(AuthStatus.Success, null, null);
            }

            // Fallback: parse raw JSON
            if (TryParseCurrentUserFromRaw(resp.RawContent, out var userId, out var displayName, out var required)
                && required.Count == 0)
            {
                _me = new InstanceManager.Core.Auth.CurrentUser(userId!, displayName ?? userId!);
                return new AuthResult(AuthStatus.Success, null, null);
            }

            // If it “needs 2FA again” or can’t parse, treat as invalid session
            await _cookieStore.ClearAsync(ct).ConfigureAwait(false);
            return new AuthResult(AuthStatus.Failed, "Saved session is not valid anymore.", null);
        }
        catch (ApiException ex) when (ex.ErrorCode == 429)
        {
            return new AuthResult(AuthStatus.Failed, "Too many attempts from your network. Please wait a few minutes and try again.", null);
        }
        catch (ApiException ex) when (ex.ErrorCode is 401 or 403)
        {
            // expired/invalid cookie
            await _cookieStore.ClearAsync(ct).ConfigureAwait(false);
            return new AuthResult(AuthStatus.Failed, "Saved session expired. Please log in again.", null);
        }
        catch (Exception ex)
        {
            return new AuthResult(AuthStatus.Failed, ex.Message, null);
        }
    }

    private static bool TryParseCurrentUserFromRaw(
        string? raw,
        out string? userId,
        out string? displayName,
        out List<TwoFactorMethod> required)
    {
        userId = null;
        displayName = null;
        required = new List<TwoFactorMethod>();

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                userId = idProp.GetString();

            if (root.TryGetProperty("displayName", out var dnProp) && dnProp.ValueKind == JsonValueKind.String)
                displayName = dnProp.GetString();

            if (root.TryGetProperty("requiresTwoFactorAuth", out var tfaProp) && tfaProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tfaProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var s = item.GetString() ?? "";

                    if (s.Equals("emailOtp", StringComparison.OrdinalIgnoreCase))
                        required.Add(TwoFactorMethod.EmailOtp);
                    else if (s.Equals("totp", StringComparison.OrdinalIgnoreCase))
                        required.Add(TwoFactorMethod.Totp);
                    else if (s.Equals("otp", StringComparison.OrdinalIgnoreCase))
                        required.Add(TwoFactorMethod.RecoveryCode);
                }
            }

            return !string.IsNullOrWhiteSpace(userId);
        }
        catch
        {
            return false;
        }
    }

    private async Task TryApplySavedCookiesAsync(CancellationToken ct)
    {
        if (_client is null || _config is null) return;

        var cookieHeader = await _cookieStore.LoadCookieHeaderAsync(ct).ConfigureAwait(false);
        Console.WriteLine($"[Auth] TryApplySavedCookies: loaded header length={cookieHeader?.Length ?? 0}");
        if (string.IsNullOrWhiteSpace(cookieHeader)) return;

        // ApiClient in this SDK sends default headers. We'll attach Cookie header.
        if (_config.DefaultHeaders.ContainsKey("Cookie"))
            _config.DefaultHeaders["Cookie"] = cookieHeader;
        else
            _config.DefaultHeaders.Add("Cookie", cookieHeader);
    }

    private async Task PersistCookiesFromResponseAsync(ApiResponse<CurrentUser> resp, CancellationToken ct)
    {
        if (_config is null) return;

        // 1) Try explicit cookies on the response object (VRChat.API exposes these separately)
        string? cookieHeader = null;
        var pairs = new List<string>();

        if (resp.Cookies is { Count: > 0 })
        {
            pairs.AddRange(
                resp.Cookies
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => $"{c.Name}={c.Value}")
            );
        }

        // 2) Try Set-Cookie headers
        if (resp.Headers is not null)
        {
            pairs.AddRange(
                resp.Headers
                    .Where(kvp => string.Equals(kvp.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                    .Where(kvp => kvp.Value is not null)
                    .SelectMany(kvp => kvp.Value!)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(sc => sc.Split(';', 2)[0].Trim())
            );
        }

        if (pairs.Count > 0)
        {
            var distinct = pairs
                .Where(p => p.Contains('='))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (distinct.Length > 0)
                cookieHeader = string.Join("; ", distinct);
        }

        // 3) Fallback: CookieContainer from SDK (this is the important part)
        cookieHeader ??= TryGetCookieHeaderFromCookieContainer();

        // 4) Fallback: whatever is already set
        if (string.IsNullOrWhiteSpace(cookieHeader) && _config.DefaultHeaders.TryGetValue("Cookie", out var existing))
            cookieHeader = existing;

        if (string.IsNullOrWhiteSpace(cookieHeader))
            return;

        await _cookieStore.SaveCookieHeaderAsync(cookieHeader, ct).ConfigureAwait(false);

        if (_config.DefaultHeaders.ContainsKey("Cookie"))
            _config.DefaultHeaders["Cookie"] = cookieHeader;
        else
            _config.DefaultHeaders.Add("Cookie", cookieHeader);
    }

    private string? TryGetCookieHeaderFromCookieContainer()
    {
        var cc = TryFindCookieContainer(_client) ?? TryFindCookieContainer(_config);
        if (cc is null) return null;

        var a = cc.GetCookieHeader(new Uri("https://api.vrchat.cloud/"));
        if (!string.IsNullOrWhiteSpace(a)) return a;

        var b = cc.GetCookieHeader(new Uri("https://vrchat.com/"));
        if (!string.IsNullOrWhiteSpace(b)) return b;

        return null;
    }

    private static CookieContainer? TryFindCookieContainer(object? obj)
    {
        if (obj is null) return null;

        var p = obj.GetType().GetProperty("CookieContainer");
        if (p is not null && typeof(CookieContainer).IsAssignableFrom(p.PropertyType))
            return p.GetValue(obj) as CookieContainer;

        var rcProp = obj.GetType().GetProperty("RestClient");
        if (rcProp?.GetValue(obj) is { } rc)
        {
            var ccProp = rc.GetType().GetProperty("CookieContainer");
            if (ccProp is not null && typeof(CookieContainer).IsAssignableFrom(ccProp.PropertyType))
                return ccProp.GetValue(rc) as CookieContainer;
        }

        return null;
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct)
    {
        _me = null;
        _currentUser = null;

        // Build config + client (cookies live inside ApiClient instance)
        _config = new Configuration
        {
            Username = username,
            Password = password,

            // Must identify your app via UserAgent, otherwise VRChat rejects requests. :contentReference[oaicite:3]{index=3}
            // (Don't put an email here; some header parsers complain)
            UserAgent = "InstanceManagerForVRChat/0.1.0 github"
        };

        _client = new ApiClient();
        _authApi = new AuthenticationApi(_client, _client, _config);

        try
        {
            await TryApplySavedCookiesAsync(ct).ConfigureAwait(false);

            // GetCurrentUserWithHttpInfo logs in and gives us raw response info (used for 2FA detection). :contentReference[oaicite:4]{index=4}
            var resp = await _rateLimiter.RunAsync(
                _ => Task.Run(() => _authApi.GetCurrentUserWithHttpInfo(), ct),
                ct
            ).ConfigureAwait(false);
            CaptureLastCurrentUserRaw(resp);

            _currentUser = resp.Data;

            // Detect 2FA requirements. Many examples check RawContent for "emailOtp" vs "totp". :contentReference[oaicite:5]{index=5}
            var required = DetectRequiredTwoFactorFromRaw(resp.RawContent);
            if (required.Count > 0)
                return new AuthResult(AuthStatus.RequiresTwoFactor, null, required);

            if (resp.Data is not null)
            {
                _currentUser = resp.Data;
                try { await PersistCookiesFromResponseAsync(resp, ct).ConfigureAwait(false); } catch { }
                _me = new InstanceManager.Core.Auth.CurrentUser(_currentUser.Id, _currentUser.DisplayName);
                return new AuthResult(AuthStatus.Success, null, null);
            }

            if (!TryParseCurrentUserFromRaw(resp.RawContent, out var userId, out var displayName, out _))
                return new AuthResult(AuthStatus.Failed, "Login response could not be parsed.", null);

            try { await PersistCookiesFromResponseAsync(resp, ct).ConfigureAwait(false); } catch { }
            _me = new InstanceManager.Core.Auth.CurrentUser(userId!, displayName ?? userId!);
            return new AuthResult(AuthStatus.Success, null, null);
        }
        catch (ApiException ex) when (ex.ErrorCode == 429)
        {
            return new AuthResult(AuthStatus.Failed, "Too many login attempts from your network. Please wait a few minutes and try again.", null);
        }
        catch (Exception ex)
        {
            return new AuthResult(AuthStatus.Failed, ex.Message, null);
        }
    }

    public async Task<AuthResult> SubmitTwoFactorAsync(TwoFactorMethod method, string code, CancellationToken ct)
    {
        if (_authApi is null)
            return new AuthResult(AuthStatus.Failed, "Not logged in. Please login first.", null);

        if (string.IsNullOrWhiteSpace(code))
            return new AuthResult(AuthStatus.Failed, "2FA code is required.", null);

        try
        {
            await _rateLimiter.RunAsync(
                _ => Task.Run(() =>
                {
                    switch (method)
                    {
                        case TwoFactorMethod.EmailOtp:
                            _authApi.Verify2FAEmailCode(new TwoFactorEmailCode(code));
                            break;

                        case TwoFactorMethod.Totp:
                            _authApi.Verify2FA(new TwoFactorAuthCode(code));
                            break;

                        case TwoFactorMethod.RecoveryCode:
                            _authApi.VerifyRecoveryCode(new TwoFactorAuthCode(code));
                            break;

                        default:
                            throw new InvalidOperationException("Unknown 2FA method.");
                    }
                }, ct),
                ct
            ).ConfigureAwait(false);

            var resp = await _rateLimiter.RunAsync(
                _ => Task.Run(() => _authApi.GetCurrentUserWithHttpInfo(), ct),
                ct
            ).ConfigureAwait(false);
            CaptureLastCurrentUserRaw(resp);

            // Prefer strongly typed model if it exists
            if (resp.Data is not null)
            {
                _currentUser = resp.Data;

                var required = DetectRequiredTwoFactorFromRaw(resp.RawContent);
                if (required.Count > 0)
                    return new AuthResult(AuthStatus.RequiresTwoFactor, "2FA still required.", required);

                try { await PersistCookiesFromResponseAsync(resp, ct).ConfigureAwait(false); } catch { }

                _me = new InstanceManager.Core.Auth.CurrentUser(_currentUser.Id, _currentUser.DisplayName);
                return new AuthResult(AuthStatus.Success, null, null);
            }

            // Fallback: parse RawContent when Data == null
            if (!TryParseCurrentUserFromRaw(resp.RawContent, out var userId, out var displayName, out var required2))
            {
                return new AuthResult(AuthStatus.Failed, "2FA verify succeeded but user payload could not be parsed.", null);
            }

            if (required2.Count > 0)
                return new AuthResult(AuthStatus.RequiresTwoFactor, "2FA still required.", required2);

            try { await PersistCookiesFromResponseAsync(resp, ct).ConfigureAwait(false); } catch { }

            _me = new InstanceManager.Core.Auth.CurrentUser(userId!, displayName ?? userId!);
            return new AuthResult(AuthStatus.Success, null, null);
        }
        catch (ApiException ex) when (ex.ErrorCode == 429)
        {
            return new AuthResult(AuthStatus.Failed, "Too many login attempts from your network. Please wait a few minutes and try again.", null);
        }
        catch (Exception ex)
        {
            // Use ToString() temporarily while debugging so you see where it actually happens
            return new AuthResult(AuthStatus.Failed, ex.ToString(), null);
        }
    }

    private void CaptureLastCurrentUserRaw(ApiResponse<CurrentUser> resp)
    {
        if (!string.IsNullOrWhiteSpace(resp.RawContent))
        {
            LastCurrentUserRawJson = resp.RawContent;
            return;
        }

        if (resp.Data is not null)
        {
            try
            {
                LastCurrentUserRawJson = JsonSerializer.Serialize(resp.Data);
            }
            catch
            {
                LastCurrentUserRawJson = null;
            }
        }
    }

    public Task<InstanceManager.Core.Auth.CurrentUser?> GetCurrentUserAsync(CancellationToken ct)
        => Task.FromResult(_me);

    public async Task LogoutAsync(CancellationToken ct)
    {
        _me = null;
        _currentUser = null;

        await _cookieStore.ClearAsync(ct).ConfigureAwait(false);

        if (_authApi is not null)
        {
            try
            {
                await _rateLimiter.RunAsync(
                    _ => Task.Run(() => _authApi.Logout(), ct),
                    ct
                ).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        _authApi = null;
        _client = null;
        _config = null;
    }

    private static List<TwoFactorMethod> DetectRequiredTwoFactorFromRaw(string? rawContent)
    {
        var required = new List<TwoFactorMethod>();
        if (string.IsNullOrWhiteSpace(rawContent))
            return required;

        // VRChat returns a field like "requiresTwoFactorAuth": ["emailOtp","totp"]
        // We just look for tokens. This works across SDK model differences.
        if (rawContent.Contains("requiresTwoFactorAuth", StringComparison.OrdinalIgnoreCase))
        {
            if (rawContent.Contains("emailOtp", StringComparison.OrdinalIgnoreCase))
                required.Add(TwoFactorMethod.EmailOtp);

            if (rawContent.Contains("totp", StringComparison.OrdinalIgnoreCase))
                required.Add(TwoFactorMethod.Totp);

            // Some responses mention "otp" (recovery) depending on API behavior/version
            if (rawContent.Contains("\"otp\"", StringComparison.OrdinalIgnoreCase))
                required.Add(TwoFactorMethod.RecoveryCode);
        }

        return required;
    }
}
