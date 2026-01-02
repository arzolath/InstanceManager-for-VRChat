using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using InstanceManager.App.ViewModels;
using InstanceManager.App.Views;
using InstanceManager.Core.Auth;
using InstanceManager.Core.Navigation;
using InstanceManager.Core.RateLimiting;
using InstanceManager.VRChat.Auth;
using Microsoft.Extensions.DependencyInjection;
using VRChat.API.Api;
using InstanceManager.VRChat;
using InstanceManager.VRChat.Blocks;
using InstanceManager.Core.Blocks;
using InstanceManager.Storage.Blocks;
using InstanceManager.App.Services;
using InstanceManager.Core.Errors;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace InstanceManager.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<InstanceManager.Core.RateLimiting.IApiRateLimiter, InstanceManager.Core.RateLimiting.ApiRateLimiter>();
        services.AddSingleton<InstanceManager.Core.RateLimiting.IAuthRateLimiter, InstanceManager.Core.RateLimiting.AuthRateLimiter>();
        services.AddSingleton<InstanceManager.App.Services.ISessionState, InstanceManager.App.Services.SessionState>();

        services.AddSingleton<IMainWindowAccessor, MainWindowAccessor>();
        services.AddSingleton<IExceptionReporter, AvaloniaExceptionReporter>();
        services.AddSingleton<INavigationService>(sp => new NavigationService(sp, sp.GetRequiredService<IExceptionReporter>()));

        // Storage
        services.AddSingleton<InstanceManager.Storage.Auth.ICookieStore, InstanceManager.Storage.Auth.FileCookieStore>();
        services.AddSingleton<InstanceManager.Storage.Blocks.IVrchatBlockCache, InstanceManager.Storage.Blocks.FileVrchatBlockCache>();
        services.AddSingleton<ICustomBlockStore, FileCustomBlockStore>();

        // Auth (single concrete instance, shared for both interfaces)
        services.AddSingleton<InstanceManager.VRChat.Auth.VRChatAuthService>();
        services.AddSingleton<InstanceManager.Core.Auth.IAuthService>(sp => sp.GetRequiredService<InstanceManager.VRChat.Auth.VRChatAuthService>());
        services.AddSingleton<IVrchatApiContext>(sp => sp.GetRequiredService<InstanceManager.VRChat.Auth.VRChatAuthService>());

        // VRChat API wrappers (requires logged-in context at call time)
        services.AddTransient(sp =>
        {
            var ctx = sp.GetRequiredService<IVrchatApiContext>();
            return new PlayermoderationApi(ctx.Client, ctx.Client, ctx.Config);
        });
        services.AddTransient<VrchatBlockApi>();

        // App services
        services.AddSingleton<IBlockListService, BlockListService>();

        // ViewModels
        services.AddSingleton<AppRootViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BlockListsViewModel>();

        Services = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var root = Services.GetRequiredService<AppRootViewModel>();
            root.Current = Services.GetRequiredService<LoginViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = root
            };

            var accessor = Services.GetRequiredService<IMainWindowAccessor>();
            accessor.MainWindow = desktop.MainWindow;

            var reporter = Services.GetRequiredService<IExceptionReporter>();

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                reporter.Report(e.Exception, "UI thread unhandled exception");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                reporter.Report(e.Exception, "Unobserved task exception");
                e.SetObserved();
            };

            // Startup: try to restore previous session via saved cookies.
            // If valid -> go straight into the app. If not -> stay on login.
            var auth = Services.GetRequiredService<IAuthService>();
            var shell = Services.GetRequiredService<ShellViewModel>();
            var nav = Services.GetRequiredService<INavigationService>();
            var session = Services.GetRequiredService<ISessionState>();
            var vrchatAuth = auth as VRChatAuthService;

            _ = TryRestoreAsync();

            async Task TryRestoreAsync()
            {
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var result = await auth.TryRestoreSessionAsync(cts.Token);

                    if (result.Status == AuthStatus.Success)
                    {
                        var me = await auth.GetCurrentUserAsync(cts.Token);
                        var raw = vrchatAuth?.LastCurrentUserRawJson;
                        var avatar = ViewModelBase.AvatarUrlFromRaw(raw);
                        Console.WriteLine($"[Startup] raw length={raw?.Length ?? 0}");
                        if (!string.IsNullOrWhiteSpace(raw))
                            Console.WriteLine($"[Startup] raw={raw}");

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            session.SetLoggedIn(me?.DisplayName ?? "Logged in", avatar, raw);
                            root.Current = shell;
                            nav.NavigateTo<DashboardViewModel>();
                        });
                    }
                    // else: stay on login
                }
                catch (Exception ex)
                {
                    reporter.Report(ex, "Startup session restore");
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
