using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Core.Navigation;

public interface INavigationAware
{
    Task OnNavigatedToAsync(CancellationToken ct);
}
