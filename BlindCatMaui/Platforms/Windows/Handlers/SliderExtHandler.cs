using BlindCatMaui.SDControls;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace BlindCatMaui.Platforms.Windows.Handlers;

public class SliderExtHandler : SliderHandler
{
    //protected override Microsoft.UI.Xaml.Controls.Slider CreatePlatformView()
    //{
    //    return new SliderNativeExt();
    //}
    private PointerEventHandler? _pointerPressedHandler;

    protected override void ConnectHandler(Microsoft.UI.Xaml.Controls.Slider platformView)
    {
        base.ConnectHandler(platformView);

        _pointerPressedHandler = new PointerEventHandler(OnPointerPressed);
        platformView.AddHandler(UIElement.PointerPressedEvent, _pointerPressedHandler, true);
    }

    protected override void DisconnectHandler(Microsoft.UI.Xaml.Controls.Slider platformView)
    {
        base.DisconnectHandler(platformView);
        platformView.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressedHandler);
        _pointerPressedHandler = null;
    }

    private void OnPointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (VirtualView is SliderExt v)
        {
            var pointerPosition = e.GetCurrentPoint(PlatformView).Position;
            // Вычисляем новое значение слайдера на основе позиции клика
            double relativePosition = pointerPosition.X / v.Width;
            double newValue = v.Minimum + relativePosition * (v.Maximum - v.Minimum);

            // Устанавливаем новое значение слайдера
            v.PassJumped(newValue);
        }
    }
}
