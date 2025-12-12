using Blazored.LocalStorage;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Manta.Services;
using MudBlazor.Services;
using ZXing.Net.Maui.Controls;
using HavenoSharp.Extensions;

namespace Manta;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<WebView, CustomWebViewHandler>();
        });

        builder.Services.AddMudServices();
        builder.Services.AddBlazoredLocalStorage();
        builder.Services.AddSingleton<DaemonInfoSingleton>();
        builder.Services.AddSingleton<BalanceSingleton>();
        builder.Services.AddSingleton<NotificationSingleton>();
        builder.Services.AddSingleton<TradeStatisticsSingleton>();
        builder.Services.AddSingleton<DaemonConnectionSingleton>();

        builder.Services.AddHavenoServices();

#if ANDROID
        builder.Services.AddTransient<INotificationManagerService, AndroidNotificationManagerService>();
        builder.Services.AddScoped<IHavenoDaemonService, AndroidHavenoDaemonService>();
#elif WINDOWS
        builder.Services.AddTransient<INotificationManagerService, WindowsNotificationManagerService>();
        builder.Services.AddScoped<IHavenoDaemonService, WindowsHavenoDaemonService>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
