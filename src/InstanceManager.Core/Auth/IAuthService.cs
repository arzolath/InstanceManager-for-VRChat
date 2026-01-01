using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Core.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct);
    Task<AuthResult> SubmitTwoFactorAsync(TwoFactorMethod method, string code, CancellationToken ct);
    Task<AuthResult> TryRestoreSessionAsync(CancellationToken ct);
    Task<CurrentUser?> GetCurrentUserAsync(CancellationToken ct);
    Task LogoutAsync(CancellationToken ct);
}

public enum AuthStatus
{
    Success,
    RequiresTwoFactor,
    Failed
}

public enum TwoFactorMethod
{
    Totp,
    EmailOtp,
    RecoveryCode
}

public sealed record AuthResult(
    AuthStatus Status,
    string? ErrorMessage,
    IReadOnlyList<TwoFactorMethod>? RequiredMethods
);

public sealed record CurrentUser(string UserId, string DisplayName);
