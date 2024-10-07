using BlindCatCore.Core;
using BlindCatCore.Models;

namespace BlindCatMaui.SDControls;

public partial class LoadingDescription : IDisposable
{
    private BaseVm? _vm;
    private LoadingStrDesc? _load;

    public LoadingDescription()
	{
		InitializeComponent();
        buttonCancel.TapCommand = new Command(ActionCancel);
    }

    public static readonly BindableProperty TokenProperty = BindableProperty.Create(
        nameof(Token),
        typeof(string),
        typeof(LoadingDescription),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is LoadingDescription self)
                self.Update(n as string);
        });
    public string? Token
    {
        get => GetValue(TokenProperty) as string;
        set => SetValue(TokenProperty, value);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_vm != null)
        {
            _vm.LoadingChanged -= Vm_LoadingChanged;
            _vm = null;
        }

        if (BindingContext is BaseVm vm)
        {
            _vm = vm;
            _vm.LoadingChanged += Vm_LoadingChanged;
        }

        Update(Token);
    }

    private void Vm_LoadingChanged(BaseVm vm, bool flag, string token)
    {
        Update(Token);
    }

    private void ActionCancel(object context)
    {
        if (context is CancellationTokenSource src)
            src.Cancel();
    }

    public void Dispose()
    {
        if (_vm != null)
        {
            _vm.LoadingChanged -= Vm_LoadingChanged;
            _vm = null;
        }

        if (_load != null)
        {
            _load.BodyChanged -= BodyChanged;
            _load = null;
        }
    }

    private void BodyChanged(object? invoker, string? newBody)
    {
        bodyScroller.IsVisible = newBody != null;
        bodyLabel.Text = newBody;
        if (bodyScroller.IsVisible)
        {
            bodyScroller.ScrollToAsync(bodyLabel, ScrollToPosition.End, false);
        }
    }

    private async void Update(string? token)
    {
        bool show = false;
        CancellationTokenSource? useCancel = null;
        LoadingStrDesc? load = null;
        var old = _load;

        if (_vm != null && token != null)
        {
            load = _vm.LoadingCheck(token);
            if (load != null)
            {
                show = true;
                useCancel = load.Cancellation;
                _load = load;
            }
        }

        if (old != load)
        {
            if (old != null)
                old.BodyChanged -= BodyChanged;

            if (load != null)
                load.BodyChanged += BodyChanged;
        }

        if (show)
        {
            IsVisible = true;
            Opacity = 1;

            labelDesc.Text = load.Description;
            labelDesc.IsVisible = load.Description != null;

            bodyScroller.IsVisible = load.Body != null;
            bodyLabel.Text = load.Body;

            buttonCancel.IsVisible = useCancel != null;
            if (useCancel != null)
            {
                buttonCancel.IsClickable = true;
                buttonCancel.TapCommandParameter = useCancel;
            }
        }
        else
        {
            if (IsLoaded)
                await this.FadeTo(0, 190);

            IsVisible = false;
            buttonCancel.IsClickable = false;
        }
    }
}