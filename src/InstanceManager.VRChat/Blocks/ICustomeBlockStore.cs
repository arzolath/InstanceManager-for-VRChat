using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Storage.Blocks;

public interface ICustomBlockStore
{
    Task<IReadOnlyCollection<string>> LoadAsync(string ownerUserId, CancellationToken ct);
    Task SaveAsync(string ownerUserId, IReadOnlyCollection<string> blockedUserIds, CancellationToken ct);
}
