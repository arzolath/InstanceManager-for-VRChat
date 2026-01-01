using System.Threading.Tasks;

namespace InstanceManager.App.ViewModels;

public interface ILoadOnceViewModel
{
    Task EnsureLoadedAsync();
}
