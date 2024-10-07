using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using BlindCatAvalonia.Desktop.Implementations;
using BlindCatAvalonia.Services;
using BlindCatAvalonia.Tools;
using BlindCatCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlindCatAvalonia.Desktop;

class Program
{
    public static bool isEntryPoint;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        isEntryPoint = true;
        ServicesDI(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        if (!isEntryPoint)
        {
            ServicesDI_Designer([]);
        }

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
            //.UseReactiveUI();
    }

    private static void ServicesDI(string[] args)
    {
        App.Services
            .AddSingleton<IAppEnv>(new AppEnv { AppLaunchedArgs = args.FirstOrDefault() })
            .AddScoped<IViewPlatforms, DesktopPlatform>()
            .AddScoped<INavigationService, DesktopNavigation>()
            .AddScoped<ICrypto, DesktopCrypto>()
            .AddScoped<IConfig, FileConfig>()
            .AddScoped<IFFMpegService, FFMpegService>()
            .AddScoped<IAudioService, WindowsAudio>();
    }

    private static void ServicesDI_Designer(string[] args)
    {
        App.Services
            .AddSingleton<IAppEnv>(new AppEnv { AppLaunchedArgs = "" })
            .AddScoped<IViewPlatforms, DesktopPlatform>()
            .AddScoped<INavigationService, DesignNavigationService>()
            .AddScoped<ICrypto, DesktopCrypto>()
            .AddScoped<IConfig, FileConfig>()
            .AddScoped<IFFMpegService, FFMpegService>()
            .AddScoped<IAudioService, WindowsAudio>();
    }
}