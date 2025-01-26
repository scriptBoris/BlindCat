using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace BlindCatAvalonia.SDcontrols;

public class Slider : Avalonia.Controls.Slider
{
    private bool isSliderThumbDrugged;
    private Thumb? thumb;
    private IDisposable? _decreaseButtonPressDispose;
    private IDisposable? _decreaseButtonReleaseDispose;
    private IDisposable? _increaseButtonSubscription;
    private IDisposable? _increaseButtonReleaseDispose;

    public event EventHandler<bool>? IsSliderThumbPressed;
    public bool IsSliderThumbDrugged { get; private set; }
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.Slider);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Desubscribe();

        thumb = e.NameScope.Find<Thumb>("thumb");
        if (thumb != null)
        {
            thumb.DragStarted += Thumb_DragStarted;
            thumb.DragCompleted += Thumb_DragCompleted;
        }

        var _track = e.NameScope.Find<Track>("PART_Track");
        if (_track != null)
        {
            _track.IgnoreThumbDrag = true;
            _track.Height = 30;

            var _decreaseButton = e.NameScope.Find<Button>("PART_DecreaseButton");
            var _increaseButton = e.NameScope.Find<Button>("PART_IncreaseButton");

            if (_decreaseButton != null)
            {
                _decreaseButtonPressDispose = _decreaseButton.AddDisposableHandler(PointerPressedEvent, TrackPressed, RoutingStrategies.Tunnel);
                _decreaseButtonReleaseDispose = _decreaseButton.AddDisposableHandler(PointerReleasedEvent, TrackReleased, RoutingStrategies.Tunnel);
            }

            if (_increaseButton != null)
            {
                _increaseButtonSubscription = _increaseButton.AddDisposableHandler(PointerPressedEvent, TrackPressed, RoutingStrategies.Tunnel);
                _increaseButtonReleaseDispose = _increaseButton.AddDisposableHandler(PointerReleasedEvent, TrackReleased, RoutingStrategies.Tunnel);
            }
        }

        //var grid = e.NameScope.Find<Grid>("HorizontalTemplate");
        //grid.RowDefinitions = new RowDefinitions
        //{
        //    new RowDefinition(0, GridUnitType.Pixel),
        //    new RowDefinition(0, GridUnitType.Auto),
        //    new RowDefinition(0, GridUnitType.Pixel),
        //};
    }

    private void Thumb_DragCompleted(object? sender, VectorEventArgs e)
    {
        isSliderThumbDrugged = false;
        Slider_Pressed(false);
    }

    private void Thumb_DragStarted(object? sender, VectorEventArgs e)
    {
        isSliderThumbDrugged = true;
        Slider_Pressed(true);
    }

    private void Desubscribe()
    {
        if (thumb != null)
        {
            thumb.DragStarted -= Thumb_DragStarted;
            thumb.DragCompleted -= Thumb_DragCompleted;
        }

        _decreaseButtonPressDispose?.Dispose();
        _decreaseButtonReleaseDispose?.Dispose();
        _increaseButtonSubscription?.Dispose();
        _increaseButtonReleaseDispose?.Dispose();
    }

    private void Slider_Pressed(bool flag)
    {
        IsSliderThumbDrugged = flag;
        IsSliderThumbPressed?.Invoke(this, flag);
    }

    private void TrackReleased(object? sender, PointerReleasedEventArgs e)
    {
        isSliderThumbDrugged = false;
        Slider_Pressed(false);
    }

    private void TrackPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            isSliderThumbDrugged = true;
            Slider_Pressed(true);
        }
    }
}