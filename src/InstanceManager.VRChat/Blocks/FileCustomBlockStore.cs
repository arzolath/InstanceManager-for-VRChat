using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Storage.Blocks;

public sealed class FileCustomBlockStore : ICustomBlockStore
{
    private readonly string _path;

    public FileCustomBlockStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InstanceManagerForVRChat"
        );
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "custom-blocks.json");
    }

    public async Task<IReadOnlyCollection<string>> LoadAsync(string ownerUserId, CancellationToken ct)
    {
        var doc = await ReadDocAsync(ct).ConfigureAwait(false);
        if (!doc.TryGetValue(ownerUserId, out var list)) return Array.Empty<string>();
        return list.Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task SaveAsync(string ownerUserId, IReadOnlyCollection<string> blockedUserIds, CancellationToken ct)
    {
        var doc = await ReadDocAsync(ct).ConfigureAwait(false);
        doc[ownerUserId] = blockedUserIds.Distinct(StringComparer.Ordinal).OrderBy(x => x).ToList();
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(doc, JsonOptions), ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, List<string>>> ReadDocAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions)
               ?? new Dictionary<string, List<string>>(StringComparer.Ordinal);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
