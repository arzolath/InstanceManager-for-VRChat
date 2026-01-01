using VRChat.API.Client;

namespace InstanceManager.VRChat;

public interface IVrchatApiContext
{
    ApiClient Client { get; }
    Configuration Config { get; }
    bool IsReady { get; }
}
