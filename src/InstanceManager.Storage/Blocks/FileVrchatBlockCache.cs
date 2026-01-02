using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.Blocks;

namespace InstanceManager.Storage.Blocks;

public sealed class FileVrchatBlockCache : IVrchatBlockCache
{
    private readonly string _path;

    public FileVrchatBlockCache()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InstanceManagerForVRChat"
        );
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "vrchat-blocks.json");
    }

    public async Task SaveAsync(IReadOnlyCollection<BlockedUser> blocks, CancellationToken ct)
    {
        var payload = blocks
            .Select(b => new BlockDoc { UserId = b.UserId, DisplayName = b.DisplayName })
            .ToArray();

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(_path, json, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<BlockedUser>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return Array.Empty<BlockedUser>();

        var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
        var doc = JsonSerializer.Deserialize<BlockDoc[]>(json, JsonOptions) ?? Array.Empty<BlockDoc>();

        return doc
            .Where(d => !string.IsNullOrWhiteSpace(d.UserId))
            .Select(d => new BlockedUser(d.UserId!, string.IsNullOrWhiteSpace(d.DisplayName) ? null : d.DisplayName))
            .ToArray();
    }

    public Task ClearAsync(CancellationToken ct)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }

    private sealed class BlockDoc
    {
        public string? UserId { get; set; }
        public string? DisplayName { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
