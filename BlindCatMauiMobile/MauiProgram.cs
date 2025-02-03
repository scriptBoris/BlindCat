using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatMauiMobile.Services;
using BlindCatMauiMobile.Views;
using Microsoft.Extensions.Logging;

namespace BlindCatMauiMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        
        // use scaffold
        ScaffoldLib.Maui.Initializer.UseScaffold(builder);
        
        // services
        builder.Services.AddScoped<IViewModelResolver, ViewModelResolver>();
        builder.Services.AddScoped<INavigationService, NavigationService>();
        builder.Services.AddScoped<IViewPlatforms, ViewPlatform>();
        builder.Services.AddScoped<IAppEnv, AppEnv>();
        builder.Services.AddScoped<IStorageService, StorageService>();
        builder.Services.AddScoped<IConfig, Config>();
        builder.Services.AddScoped<IDeclaratives, Declaratives>();
        builder.Services.AddScoped<ICrypto, Crypto>();
        builder.Services.AddScoped<IDataBaseService, DatabaseService>();
        builder.Services.AddScoped<IFFMpegService, FFMpegService>();
        
        UsingServices.AddInternalServices(builder.Services);
        
        // navigations
        builder.Services.RegisterNav<HomeVm, HomeView>();
        builder.Services.RegisterNav<MediaPresentVm, MediaPresentView>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}