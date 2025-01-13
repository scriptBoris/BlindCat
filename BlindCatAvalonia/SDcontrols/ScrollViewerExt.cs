using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.SDcontrols;

public class ScrollViewerExt : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.ScrollViewer);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        //base.OnPointerWheelChanged(e);

        var newOffset = Offset.Y - e.Delta.Y * 20.0;

        Offset = new Vector(
            Offset.X,
            Math.Clamp(newOffset, 0, Extent.Height - Viewport.Height)
        );

        //e.Handled = true;
    }
}