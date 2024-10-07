using ButtonSam.Maui.Core;

namespace BlindCatMaui.SDControls;

public class Button : ButtonSam.Maui.Button
{
    public event EventHandler? Clicked;

    protected override void AnimationFrame(double x)
    {
        base.AnimationFrame(x);
        Content.Opacity = 1 - x * 0.5;
    }

    protected override void AnimationPressedRestore(float x, float y)
    {
        base.AnimationPressedRestore(x, y);
        Content.Opacity = 1;
    }

    protected override void CallbackRelease(CallbackEventArgs args)
    {
        base.CallbackRelease(args);
        Content.Opacity = 1;
    }

    protected override void OnTapCompleted()
    {
        base.OnTapCompleted();
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
