using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Storage.Auth;

public sealed class FileCookieStore : ICookieStore
{
    private readonly string _path;

    public FileCookieStore(string appName = "InstanceManagerForVRChat")
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName
        );
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "cookies.json");
    }

    public async Task<string?> LoadCookieHeaderAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return null;

        var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
        var doc = JsonSerializer.Deserialize<CookieDoc>(json);
        return doc?.CookieHeader;
    }

    public async Task SaveCookieHeaderAsync(string cookieHeader, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new CookieDoc(cookieHeader));
        await File.WriteAllTextAsync(_path, json, ct).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken ct)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }

    private sealed record CookieDoc(string CookieHeader);
}
