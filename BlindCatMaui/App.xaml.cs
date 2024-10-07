using BlindCatCore.Core;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatMaui.Core;
using ScaffoldLib.Maui.Toolkit;

namespace BlindCatMaui;

public partial class App : Application
{
    public static string? InitFilePath;
    private readonly INavigationService _navigationService;

    public static IServiceProvider ServiceProvider { get; set; } = null!;

    public static event EventHandler<bool>? KeyCtrl_Down;
    public static event EventHandler<bool>? KeyCtrl_Up;
    public static event EventHandler<bool>? KeyAlt_Down;
    public static event EventHandler<bool>? KeyAlt_Up;
    public static event EventHandler<Size>? AppSizeChanged;
    
    public App(IViewModelResolver resolver, INavigationService nav, IAppEnv appEnv, IServiceProvider sp)
    {
        ServiceProvider = sp;
        appEnv.AppLaunchedArgs = InitFilePath;

        InitializeComponent();
        var vm = resolver.Resolve(new HomeVm.Key { });
        nav.UseRootView(vm.View);

        MainPage = (Page)nav.MainPage;
        _navigationService = nav;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var w = base.CreateWindow(activationState);
#if DEBUG
        w.X = 2200;
        w.Y = 0;
        w.Width = 800;
#endif

#if WINDOWS
        w.Title = "Blind Cat";
        Task.Run(async () =>
        {
            var h = await w.AwaitHandler();
            var n = h.PlatformView as Microsoft.UI.Xaml.Window;
            
        });
#endif
        return w;
    }

#if ANDROID

    public static bool IsCtrlPressed { get; }
    public static bool IsAltPressed { get; }

#elif WINDOWS
    public static bool IsCtrlPressed => Platforms.Windows.Native.Win32Native.IsCtrlPressed();
    public static bool IsAltPressed => Platforms.Windows.Native.Win32Native.IsAltPressed();

    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case global::Windows.System.VirtualKey.Left:
                PassKeyCombo("Left");
                break;
            case global::Windows.System.VirtualKey.Right:
                PassKeyCombo("Right");
                break;
            case global::Windows.System.VirtualKey.Menu:
                KeyAlt_Down?.Invoke(this, true);
                break;
            case global::Windows.System.VirtualKey.Enter:
                if (IsCtrlPressed)
                    PassKeyCombo("Ctrl+Enter");
                else
                    PassKeyCombo("Enter");
                break;
            case global::Windows.System.VirtualKey.Control:
            case global::Windows.System.VirtualKey.LeftControl:
            case global::Windows.System.VirtualKey.RightControl:
                KeyCtrl_Down?.Invoke(this, true);
                break;
            case global::Windows.System.VirtualKey.Escape:
                PassKeyCombo("Esc");
                break;
            case global::Windows.System.VirtualKey.A:
                if (IsCtrlPressed)
                    PassKeyCombo("Ctrl+A");
                break;
            case global::Windows.System.VirtualKey.GoBack:
                _navigationService.Pop();
                break;
            default:
                break;
        }
    }

    private void OnKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case global::Windows.System.VirtualKey.Menu:
                KeyAlt_Up?.Invoke(this, true);
                break;
            case global::Windows.System.VirtualKey.Control:
            case global::Windows.System.VirtualKey.LeftControl:
            case global::Windows.System.VirtualKey.RightControl:
                KeyCtrl_Down?.Invoke(this, false);
                break;
            default:
                break;
        }
    }

    public void HandleWindow(Microsoft.UI.Xaml.Window n)
    {
        n.SizeChanged += (o, e) =>
        {
            AppSizeChanged?.Invoke(o, new Size(e.Size.Width, e.Size.Height));
        };

        n.Content.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyDown), true);
        n.Content.AddHandler(Microsoft.UI.Xaml.UIElement.KeyUpEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyUp), true);

        n.Content.PointerWheelChanged += (o, e) =>
        {
            var native = o as Microsoft.UI.Xaml.UIElement;
            int delta = e.GetCurrentPoint(native).Properties.MouseWheelDelta;
            if (delta > 0)
            {
                PassKeyCombo("WheelUp");
            }
            else
            {
                PassKeyCombo("WheelDown");
            }
        };

        n.Content.PointerPressed += async (o, e) =>
        {
            var p = e.GetCurrentPoint(null).Properties;

            // back
            if (p.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.XButton1Pressed)
            {
                var vm = ResolveCurrentVm();

                foreach (var childVm in vm.Children.Reverse())
                {
                    if (await childVm.TryClose())
                    {
                        await childVm.Close();
                        return;
                    }
                }

                if (await vm.TryClose())
                    await vm.Close();
            }
        };
    }

    private BaseVm ResolveCurrentVm()
    {
        var last = (View)_navigationService.CurrentView;
        var vm = (BaseVm)last.BindingContext;
        return vm;
    }

    private void PassKeyCombo(string keycombo)
    {
        var vm = ResolveCurrentVm();
        if (vm.Children.Count == 0)
        {
            vm.OnKeyComboListener(keycombo);
        }
        else
        {
            foreach (var item in vm.Children)
                item.OnKeyComboListener(keycombo);
        }
    }
#endif
}
