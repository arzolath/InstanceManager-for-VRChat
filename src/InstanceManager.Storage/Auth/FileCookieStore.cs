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
        Console.WriteLine($"[CookieStore] LoadCookieHeader from {_path}");
        if (!File.Exists(_path))
        {
            Console.WriteLine("[CookieStore] No cookie file found.");
            return null;
        }

        var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
        var doc = JsonSerializer.Deserialize<CookieDoc>(json);
        Console.WriteLine($"[CookieStore] Loaded cookie header length={doc?.CookieHeader?.Length ?? 0}");
        return doc?.CookieHeader;
    }

    public async Task SaveCookieHeaderAsync(string cookieHeader, CancellationToken ct)
    {
        Console.WriteLine($"[CookieStore] SaveCookieHeader to {_path}, length={cookieHeader?.Length ?? 0}");
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
