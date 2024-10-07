using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.Services;
using BlindCatAvalonia.Tools;
using BlindCatAvalonia.Views;
using BlindCatAvalonia.Views.Panels;
using BlindCatAvalonia.Views.Popups;
using BlindCatCore.Core;
using BlindCatCore.PopupViewModels;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatCore.ViewModels.Panels;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace BlindCatAvalonia;

public partial class App : Application
{
    private INavigationService navigation = null!;

    public static event EventHandler<bool>? OnButtonCtrl;
    public static bool IsButtonCtrlPressed => Win32Native.IsCtrlPressed();
    public static bool IsButtonLeftMousePressed { get; private set; }

    public static ServiceCollection Services { get; private set; } = new();
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services
            .AddScoped<IViewModelResolver, ViewModelResolver>()
            .AddScoped<IStorageService, StorageService>()
            .AddScoped<IDataBaseService, DataBaseService>()
            .AddScoped<IDeclaratives, Declaratives>()
            .AddScoped<IMetaDataAnalyzer, MetaDataAnalyzer>()
            .AddInternalServices()
            .AddLogging();

        RegisterNavigations(Services);
        ServiceProvider = Services.BuildServiceProvider();
        var resolver = ServiceProvider.GetRequiredService<IViewModelResolver>();
        var appEnv = ServiceProvider.GetRequiredService<IAppEnv>();
        navigation = ServiceProvider.GetRequiredService<INavigationService>();

        var vm = resolver.Resolve(new HomeVm.Key { });
        navigation.UseRootView(vm.View, false);
        var root = (Control)navigation.MainPage;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var w = new MainWindow();
            w.UseContent(root);
            Handle(w);
            desktop.MainWindow = w;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = root;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void RegisterNavigations(IServiceCollection services)
    {
        services
            .RegisterNav<HomeVm, HomeView>()
            //.RegisterNav<LocalGallsVm, LocalGallsView>()
            //.RegisterNav<GalVm, GalView>()
            .RegisterNav<MediaPresentVm, MediaPresentView>()
            .RegisterNav<DirPresentVm, DirPresentView>()
            .RegisterNav<StorageCreateVm, StorageCreateView>()
            .RegisterNav<StoragePresentVm, StoragePresentView>()
            //.RegisterNav<StorageEditVm, StorageEditView>()
            //.RegisterNav<AlbumVm, AlbumView>()
            ;

        // popups
        services.RegisterNav<EditTagsVm, EditTagsView>();
        services.RegisterNav<SaveFilesVm, SaveFilesView>();
        services.RegisterNav<EditMetaVm, EditMetaView>();

        services.RegisterNav<FileInfoPanelVm, FileInfoPanel>();
        services.RegisterNav<StorageFileInfoPanelVm, StorageFileInfoPanel>();
    }

    #region handlers
    public static void Handle(Window w)
    {
        w.AddHandler(Window.KeyDownEvent, W_KeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble | RoutingStrategies.Direct);
        w.AddHandler(Window.KeyUpEvent, W_KeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble | RoutingStrategies.Direct);
        w.PointerPressed += W_PointerPressed;
        w.PointerReleased += W_PointerReleased;
    }

    public static void Dishandle(Window w)
    {
        w.RemoveHandler(Window.KeyDownEvent, W_KeyDown);
        w.RemoveHandler(Window.KeyUpEvent, W_KeyUp);
        w.PointerPressed -= W_PointerPressed;
        w.PointerReleased -= W_PointerReleased;
    }

    private static async void W_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var v = (Window)sender!;
        //var v = (Visual)sender!;
        var properties = e.GetCurrentPoint(v).Properties;

        if (properties.IsXButton1Pressed) // Кнопка "Назад"
        {
            var vm = ResolveVm(v);
            if (await vm.TryClose())
                await vm.Close();
        }
        else if (properties.IsXButton2Pressed) // Кнопка "Вперед"
        {
            // сделать?
        }
        else if (properties.IsLeftButtonPressed)
        {
            IsButtonLeftMousePressed = true;
        }
    }

    private static void W_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        var v = (Visual)sender!;
        var properties = e.GetCurrentPoint(v).Properties;

        if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
        {
            IsButtonLeftMousePressed = false;
        }
    }

    private static async void W_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        var w = (Window)sender!;
        bool handled = false;
        switch (e.Key)
        {
            case Avalonia.Input.Key.LeftCtrl:
            case Avalonia.Input.Key.RightCtrl:
                //IsButtonCtrlPressed = true;
                OnButtonCtrl?.Invoke(w, true);
                break;
            case Avalonia.Input.Key.Escape:
                PassKey(w, "Esc", ref handled);

                var vm = ResolveVm(w);
                if (vm.IsPopup && await vm.TryClose())
                    await vm.Close();

                break;
            case Avalonia.Input.Key.Return:
                PassKey(w, "Enter", ref handled);
                break;
            case Avalonia.Input.Key.Left:
                PassKey(w, "Left", ref handled);
                break;
            case Avalonia.Input.Key.Right:
                PassKey(w, "Right", ref handled);
                break;
            default:
                string k = e.Key.ToString();
                PassKey(w, k, ref handled);
                break;
        }

        if (handled)
            e.Handled = true;
    }

    private static void W_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        var w = (Window)sender!;
        switch (e.Key)
        {
            case Avalonia.Input.Key.RightCtrl:
            case Avalonia.Input.Key.LeftCtrl:
                //IsButtonCtrlPressed = false;
                OnButtonCtrl?.Invoke(w, false);
                break;
            //case Avalonia.Input.Key.Escape:
            //    navigation.CurrentView.ResolveVm().OnKeyComboListener("Esc");
            //    break;
            //case Avalonia.Input.Key.Left:
            //    navigation.CurrentView.ResolveVm().OnKeyComboListener("Left");
            //    break;
            //case Avalonia.Input.Key.Right:
            //    navigation.CurrentView.ResolveVm().OnKeyComboListener("Right");
            //    break;
            default:
                break;
        }
    }

    private static BaseVm ResolveVm(Window w)
    {
        if (w is MainWindow)
        {
            var app = App.Current as App;
            return app.navigation.CurrentView.ResolveVm();
        }
        else if (w.DataContext is BaseVm vm)
        {
            return vm;
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    private static void PassKey(Window w, string key, ref bool handled)
    {
        string k;

        if (IsButtonCtrlPressed)
        {
            k = $"Ctrl+{key}";
        }
        else
        {
            k = key;
        }

        Debug.WriteLine($"Pressed {k}");

        var arg = new KeyPressedArgs
        {
            Key = k,
        };
        if (w is MainWindow)
        {
            var app = App.Current as App;
            app?.navigation.CurrentView.ResolveVm().OnKeyComboListener(arg);
        }
        else if (w.DataContext is BaseVm vm)
        {
            vm.OnKeyComboListener(arg);
        }

        handled = arg.Handled;
    }
    #endregion handlers
}
