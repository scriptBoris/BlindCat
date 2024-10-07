
namespace BlindCatMaui.SDControls;

public class ButtonOverAnim : ButtonSam.Maui.Button
{
    protected override void AnimationFrame(double x)
    {
        base.AnimationFrame(x);
        if (Content != null)
        {
            Content.Opacity = 1 - (x * 0.5);
        }
    }

    protected override void AnimationMouseOverRestore()
    {
        base.AnimationMouseOverRestore();
        if (Content != null)
        {
            Content.Opacity = 1;
        }
    }

    protected override void AnimationMouseOverStart()
    {
        base.AnimationMouseOverStart();

        if (Content != null)
        {
            if (IsMouseOver)
            {
                Content.Opacity = 0.5;
            }
            else
            {
                Content.Opacity = 1;
            }
        }
    }
}