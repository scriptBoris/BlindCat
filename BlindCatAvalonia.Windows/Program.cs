using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using BlindCatAvalonia.Windows.Implementations;
using BlindCatAvalonia.Services;
using BlindCatAvalonia.Tools;
using BlindCatCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlindCatAvalonia.Windows;

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
            //.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
            .LogToTrace();
            //.UseReactiveUI();
    }

    private static void ServicesDI(string[] args)
    {
        App.Services
            .AddSingleton<IAppEnv>(new AppEnv { AppLaunchedArgs = args.FirstOrDefault() })
            .AddScoped<IViewPlatforms, WindowsPlatform>()
            .AddScoped<INavigationService, DesktopNavigation>()
            .AddScoped<ICrypto, DesktopCrypto>()
            .AddScoped<IConfig, FileConfig>()
            .AddScoped<IAudioService, WindowsAudio>()
            .AddScoped<IKeyboardNative, KeyboardWin32>();
    }

    private static void ServicesDI_Designer(string[] args)
    {
        App.Services
            .AddSingleton<IAppEnv>(new AppEnv { AppLaunchedArgs = "" })
            .AddScoped<IViewPlatforms, WindowsPlatform>()
            .AddScoped<INavigationService, DesignNavigationService>()
            .AddScoped<ICrypto, DesktopCrypto>()
            .AddScoped<IConfig, FileConfig>()
            .AddScoped<IAudioService, WindowsAudio>();
    }
}