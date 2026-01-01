using System;
using InstanceManager.App.Services;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Errors;
using InstanceManager.Core.Navigation;

namespace InstanceManager.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(
        INavigationService nav,
        IAuthService auth,
        AppRootViewModel root,
        LoginViewModel login,
        ISessionState session,
        IExceptionReporter exceptions,
        IServiceProvider services)
        : base(auth, root, session, exceptions, services)
    {
        
    }
}
