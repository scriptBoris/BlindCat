using BlindCatCore.Core;
using ScaffoldLib.Maui;
using ScaffoldLib.Maui.Core;

namespace BlindCatMaui.Core;

public partial class WrapperPopup : IZBufferLayout, IAppear
{
    private readonly TaskCompletionSource<object?> tsc = new();
    private readonly View _view;
    private readonly BaseVm _vm;

    public event VoidDelegate? DeatachLayer;

    public WrapperPopup(View view)
    {
        _view = view;
        _vm = (BaseVm)view.BindingContext;
        InitializeComponent();
        containerGrid.Children.Add(view);
        Opacity = 0;

        view.Behaviors.Add(new LinkBehavior
        {
            Direct = this,
        });

        view.PropertyChanged += View_PropertyChanged;

        this.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(Close),
        });

        UpdateTitle();
    }

    private void View_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ScaffoldTitle")
        {
            UpdateTitle();
        }
    }

    private void UpdateTitle()
    {
        labelTitle.Text = Scaffold.GetTitle(_view);
        bool show = labelTitle.Text != null;
        labelTitle.IsVisible = show;
        stackLayoutSeparator.IsVisible = show;
    }

    public void Close()
    {
        DeatachLayer?.Invoke();
    }

    public Task OnHide(CancellationToken cancel)
    {
        return this.FadeTo(0);
    }

    public void OnRemoved()
    {
        _view.PropertyChanged -= View_PropertyChanged;
        tsc.TrySetResult(null);
        _vm.OnDisconnectedFromNavigation();
    }

    public Task OnShow(CancellationToken cancel)
    {
        return this.FadeTo(1);
    }

    public Task<object?> GetResult()
    {
        return tsc.Task;
    }

    public void OnAppear(bool isComplete)
    {
        if (_view is IAppear ap)
            ap.OnAppear(isComplete);
    }
}

public class LinkBehavior : Behavior
{
    public required WrapperPopup Direct { get; set; }
}

public class StackLayoutCustom : StackLayout
{
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        double freeWidth;

        if (widthConstraint < 300)
        {
            freeWidth = widthConstraint * 0.7;
        }
        else if (widthConstraint < 500)
        {
            freeWidth = 300;
        }
        else
        {
            freeWidth = 400;
        }

        var size = base.MeasureOverride(freeWidth, heightConstraint);
        double h = size.Height;
        double w = freeWidth;

        return new Size(w, h);
    }
}