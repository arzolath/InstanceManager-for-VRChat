using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Storage.Logs;

public sealed record KickLogEntry(
    DateTimeOffset Timestamp,
    string WorldId,
    string InstanceId,
    string? InstanceName,
    string PlayerId,
    string? PlayerDisplayName,
    string Action,
    string? Details)
{
    public string PlayerLabel => string.IsNullOrWhiteSpace(PlayerDisplayName) ? PlayerId : PlayerDisplayName;
    public string InstanceLabel => string.IsNullOrWhiteSpace(InstanceName) ? InstanceId : InstanceName;
}

public interface IKickLogStore
{
    Task AppendAsync(KickLogEntry entry, CancellationToken ct);
    Task<IReadOnlyList<KickLogEntry>> LoadAsync(CancellationToken ct);
}

public sealed class FileKickLogStore : IKickLogStore
{
    private readonly string _path;

    public FileKickLogStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InstanceManagerForVRChat"
        );

        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "kick-log.ndjson");
    }

    public async Task AppendAsync(KickLogEntry entry, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(_path, line, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KickLogEntry>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return Array.Empty<KickLogEntry>();

        var lines = await File.ReadAllLinesAsync(_path, ct).ConfigureAwait(false);
        var entries = new List<KickLogEntry>();

        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<KickLogEntry>(line, JsonOptions);
                if (entry is not null)
                    entries.Add(entry);
            }
            catch
            {
                // ignore malformed lines so log reading never fails
            }
        }

        return entries;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
