using BlindCatCore.Services;
using BlindCatCore.ViewModels;

namespace BlindCatMauiMobile;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();
        
        var nav = services.GetRequiredService<INavigationService>();
        var resolver = services.GetRequiredService<IViewModelResolver>();

        var homevm = resolver.Resolve(new HomeVm.Key());
        var homeview = (View)homevm.View;
        nav.UseRootView(homeview, false);
        var page = (Page)nav.MainPage;
        MainPage = page;
    }
}