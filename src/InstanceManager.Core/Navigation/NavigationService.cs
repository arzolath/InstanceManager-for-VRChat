using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InstanceManager.Core.Errors;

namespace InstanceManager.Core.Navigation;

public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;
    private readonly IExceptionReporter _exceptions;

    [ObservableProperty]
    private object _currentViewModel = null!;

    public NavigationService(IServiceProvider services, IExceptionReporter exceptions)
    {
        _services = services;
        _exceptions = exceptions;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var vm = _services.GetService(typeof(TViewModel)) as TViewModel;
        if (vm is null)
            throw new InvalidOperationException($"No service registered for {typeof(TViewModel).FullName}");

        CurrentViewModel = vm;

        if (vm is INavigationAware aware)
        {
            _ = SafeOnNavigatedToAsync(aware);
        }
    }

    private async Task SafeOnNavigatedToAsync(INavigationAware aware)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await aware.OnNavigatedToAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _exceptions.Report(ex, "Navigation: OnNavigatedToAsync");
        }
    }
}
