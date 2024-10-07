using ButtonSam.Maui.Core;
using Microsoft.Maui.Controls.Platform;

namespace BlindCatMaui.SDControls;

/// <summary>
/// Для определения прыжка
/// </summary>
public class SliderOverlay : InteractiveContainer
{
    private double sliderMinimum = 0;
    private double sliderMaximum = 1;

    public event EventHandler<double>? Jumped;

#if WINDOWS
    protected override async void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Panel n)
        {
            await Task.Delay(20);
            n.Background = new SolidColorBrush(Colors.Transparent).ToBrush();
            n.IsHitTestVisible = false;
            n.IsTapEnabled = false;
        }
    }
#endif

    protected override void CallbackCanceled(CallbackEventArgs args)
    {
    }

    protected override void CallbackEntered(CallbackEventArgs args)
    {
    }

    protected override void CallbackExited(CallbackEventArgs args)
    {
    }

    protected override void CallbackPressed(CallbackEventArgs args)
    {
        // Вычисляем новое значение слайдера на основе позиции клика
        double relativePosition = args.X / Width;
        double newValue = sliderMinimum + relativePosition * (sliderMaximum - sliderMinimum);

        Jumped?.Invoke(this, newValue);
    }

    protected override void CallbackRelease(CallbackEventArgs args)
    {
    }

    protected override void CallbackRunning(CallbackEventArgs args)
    {
    }

    protected override bool OnGestureCanceled(InteractiveEventArgs args)
    {
        return true;
    }

    protected override bool OnGestureEntered(InteractiveEventArgs args)
    {
        return true;
    }

    protected override bool OnGestureExited(InteractiveEventArgs args)
    {
        return true;
    }

    protected override bool OnGesturePressed(InteractiveEventArgs args)
    {
        return false;
    }

    protected override bool OnGestureRelease(InteractiveEventArgs args)
    {
        return false;
    }

    protected override bool OnGestureRunning(InteractiveEventArgs args)
    {
        return false;
    }
}
