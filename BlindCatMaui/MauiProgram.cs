using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatMaui.Views;
using ButtonSam.Maui;
using CommunityToolkit.Maui;
using BlindCatMaui.SDControls;
using BlindCatMaui.Services;
using BlindCatMaui.Views.Popups;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using zoft.MauiExtensions.Controls;
using Microsoft.Maui.Controls.Compatibility.Hosting;
using Sharpnado.MaterialFrame;
using RGPopup.Maui.Extensions;
using ScaffoldLib.Maui;
using Microsoft.Maui.LifecycleEvents;
using BlindCatMaui.SDControls.Elements;

namespace BlindCatMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseScaffold()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .UseZoftAutoCompleteEntry()
            .UseButtonSam()
            .UseMauiRGPopup(config =>
            {
                config.FixKeyboardOverlap = true;
            })
            .UseSharpnadoMaterialFrame(false)
            .ConfigureMauiHandlers(h =>
            {
#if WINDOWS
                h.AddHandler(typeof(SliderExt), typeof(Platforms.Windows.Handlers.SliderExtHandler));
                h.AddHandler(typeof(ToolkitVideoPlayer), typeof(Platforms.Windows.Handlers.ToolkitVideoPlayerHandler));
                h.AddHandler(typeof(AutoSuggestWinUI), typeof(Platforms.Windows.Handlers.AutoSuggestWinUIHandler));
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services
            .AddSingleton<IAppEnv, AppEnv>()
            .AddSingleton(InitFFmpegService())
            .AddScoped<ICrypto, Crypto>()
            .AddScoped<IDataBaseService, DataBaseService>()
            .AddScoped<IViewModelResolver, ViewModelResolver>()
            .AddScoped<IViewPlatforms, ViewPlatforms>()
            .AddScoped<INavigationService, NavigationService>()
            .AddScoped<IHttpLauncher, HttpLauncher>()
            .AddScoped<IStorageService, StorageService>()
            .AddScoped<IMetaDataAnalyzer, MetaDataAnalyzer>()
            .AddScoped<IMediaControllerResolver, MediaControllerResolver>()
            .AddScoped<IDeclaratives, Declaratives>()
            .AddInternalServices();

        RegisterNavigations(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

#if WINDOWS
        builder.ConfigureLifecycleEvents(x =>
        {
            x.AddWindows(w =>
            {
                w.OnWindowCreated(e =>
                {
                    var app = (App)App.Current!;
                    app.HandleWindow(e);
                });
            });
        });
#endif
        return builder.Build();
    }

    public static void RegisterNavigations(IServiceCollection services)
    {
        services
            .RegisterNav<HomeVm, HomeView>()
            .RegisterNav<LocalGallsVm, LocalGallsView>()
            .RegisterNav<GalVm, GalView>()
            .RegisterNav<MediaPresentVm, MediaPresentView>()
            .RegisterNav<DirPresentVm, DirPresentView>()
            .RegisterNav<StorageCreateVm, StorageCreateView>()
            .RegisterNav<StoragePresentVm, StoragePresentView>()
            .RegisterNav<StorageEditVm, StorageEditView>()
            .RegisterNav<AlbumVm, AlbumView>()
            ;

        // popups
        services.RegisterNav<EditTagsVm, EditTagsPopup>();
        services.RegisterNav<RemoveTagsVm, RemoveTagsPopup>();
        services.RegisterNav<SaveFilesVm, SaveFilesPopup>();
    }

    private static IFFMpegService InitFFmpegService()
    {
        string appDir;
        char s = Path.DirectorySeparatorChar;

#if WINDOWS
        string[] paths =
        [
            $"Libs{s}x64{s}ffmpeg.exe",
            $"Libs{s}x64{s}ffprobe.exe",
            $"Libs{s}x64{s}ffplay.exe",
        ];

        appDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        if (appDir == null)
            throw new ApplicationException("No found path dir for current process");

#elif ANDROID
        string[] paths =
        [
            $"Libs{s}ARM{s}ffmpeg.so",
            $"Libs{s}ARM{s}ffprobe.so",
            $"Libs{s}ARM{s}ffplay.so",
        ];

        appDir = FileSystem.AppDataDirectory;
#else
        string[] paths = null;
        appDir = null;
#endif


        string ffmpeg = Path.Combine(appDir, paths[0]);
        string ffprobe = Path.Combine(appDir, paths[1]);
        string ffplay = Path.Combine(appDir, paths[2]);

        var res = new FFMpegService
        {
            PathToFFmpegExe = ffmpeg,
            PathToFFprobeExe = ffprobe,
            PathToFFplayExe = ffplay,
        };
        return res;
    }
}
