using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Storage.Auth;

public interface ICookieStore
{
    Task<string?> LoadCookieHeaderAsync(CancellationToken ct);
    Task SaveCookieHeaderAsync(string cookieHeader, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}
