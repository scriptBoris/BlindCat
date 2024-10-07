using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.SDcontrols;

public class ZStackLayout : Panel
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var lastVisible = Children.LastOrDefault(x => x.IsVisible);
        foreach (var item in Children)
        {
            if (item == lastVisible)
            {
                item.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
                if (item.ZIndex == 0)
                    item.ZIndex = 1;
                else
                    item.ZIndex = 0;
                continue;
            }

            item.Arrange(new Rect(10000, -1000, 500, 500));
            item.ZIndex = -1;
        }

        return finalSize;
    }
}