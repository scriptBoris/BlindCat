namespace BlindCatMaui.SDControls;

public class SliderExt : Slider
{
    public event EventHandler<double>? SliderJumped;

    public void PassJumped(double newValue)
    {
        SliderJumped?.Invoke(this, newValue);
    }
}
