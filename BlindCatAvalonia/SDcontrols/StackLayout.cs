using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia.Controls.Primitives;
using System.Reflection;

namespace BlindCatAvalonia.SDcontrols;

public class StackLayout : Panel
{
    private Orientation _orientation = Orientation.Vertical;
    private double _spacing;

    public StackLayout()
    {
    }

    // padding
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<StackLayout>();
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    // orientation
    public static readonly DirectProperty<StackLayout, Orientation> OrientationProperty = AvaloniaProperty.RegisterDirect<StackLayout, Orientation>(
        nameof(Orientation),
        (self) => self._orientation,
        (self, nev) =>
        {
            self._orientation = nev;
        }
    );
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetAndRaise(OrientationProperty, ref _orientation, value);
    }

    // spacing
    public static readonly DirectProperty<StackLayout, double> SpacingProperty = AvaloniaProperty.RegisterDirect<StackLayout, double>(
        nameof(Spacing),
        (self) => self._spacing,
        (self, nev) =>
        {
            self._spacing = nev;
        }
    );
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetAndRaise(SpacingProperty, ref _spacing, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double totalWidth = Padding.Left + Padding.Right;
        double totalHeight = Padding.Top + Padding.Bottom;

        int visChilds = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
                continue;

            visChilds++;
            if (Orientation == Orientation.Vertical)
            {
                var childAvailableSize = new Size(availableSize.Width - Padding.Left - Padding.Right, double.PositiveInfinity);
                child.Measure(childAvailableSize);

                totalWidth = Math.Max(totalWidth, child.DesiredSize.Width + Padding.Left + Padding.Right);
                totalHeight += child.DesiredSize.Height;

            }
            else
            {
                var childAvailableSize = new Size(double.PositiveInfinity, availableSize.Height - Padding.Top - Padding.Bottom);
                child.Measure(childAvailableSize);

                totalWidth += child.DesiredSize.Width;
                totalHeight = Math.Max(totalHeight, child.DesiredSize.Height + Padding.Top + Padding.Bottom);

            }
            
        }

        if (Orientation == Orientation.Vertical)
        {
            totalHeight += (visChilds - 1) * Spacing;
        }
        else
        {
            totalWidth += (visChilds - 1) * Spacing;
        }

        return new Size(totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Orientation == Orientation.Horizontal)
        {
            DrawHorizontal(finalSize);
        }
        else
        {
            if (this.DesiredSize.Height < finalSize.Height)
            {
                DrawVertical(finalSize);
            }
            else
            {
                DrawVerticalEasy(finalSize);
            }
        }

        return finalSize;
    }

    protected void DrawVertical(Size finalSize)
    {
        double currentX = Padding.Left;
        double currentY = Padding.Top;
        int visChildrens = Children.Count(x => x.IsVisible);

        // find FILLs
        double availableWidth = finalSize.Width - (Padding.Right + Padding.Left);
        double availableHeight = finalSize.Height - (Padding.Top + Padding.Bottom);
        if (visChildrens > 1)
            availableHeight -= (visChildrens - 1) * Spacing;

        int countFills = Children.Count(x => x.VerticalAlignment == VerticalAlignment.Stretch && x.IsVisible);
        double freeSize = availableHeight - Children.Sum(x => x.VerticalAlignment != VerticalAlignment.Stretch ? x.DesiredSize.Height : 0);
        double fillSize = freeSize / countFills;

        // draws
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
                continue;

            Rect rect;

            double x = currentX;
            double w;

            switch (child.HorizontalAlignment)
            {
                case HorizontalAlignment.Stretch:
                    w = availableWidth;
                    break;
                case HorizontalAlignment.Left:
                    w = child.DesiredSize.Width;
                    break;
                case HorizontalAlignment.Center:
                    w = child.DesiredSize.Width;
                    x = (finalSize.Width / 2) - (w / 2);
                    break;
                case HorizontalAlignment.Right:
                    w = child.DesiredSize.Width;
                    x = availableWidth - w;
                    break;
                default:
                    throw new NotSupportedException();
            }

            double h = child.VerticalAlignment == VerticalAlignment.Stretch
                ? fillSize
                : child.DesiredSize.Height;

            rect = new Rect(x, currentY, w, h);
            currentY += h + Spacing;

            child.Arrange(rect);
        }
    }


    protected void DrawVerticalEasy(Size finalSize)
    {
        double currentX = Padding.Left;
        double currentY = Padding.Top;
        int visChildrens = Children.Count(x => x.IsVisible);

        // find FILLs
        double availableWidth = finalSize.Width - (Padding.Right + Padding.Left);
        double availableHeight = finalSize.Height - (Padding.Top + Padding.Bottom);
        if (visChildrens > 1)
            availableHeight -= (visChildrens - 1) * Spacing;

        // draws
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
                continue;

            Rect rect;
            double x = currentX;
            double w;

            switch (child.HorizontalAlignment)
            {
                case HorizontalAlignment.Stretch:
                    w = availableWidth;
                    break;
                case HorizontalAlignment.Left:
                    w = child.DesiredSize.Width;
                    break;
                case HorizontalAlignment.Center:
                    w = child.DesiredSize.Width;
                    x = (finalSize.Width / 2) - (w / 2);
                    break;
                case HorizontalAlignment.Right:
                    w = child.DesiredSize.Width;
                    x = availableWidth - w;
                    break;
                default:
                    throw new NotSupportedException();
            }

            double h = child.DesiredSize.Height;

            rect = new Rect(x, currentY, w, h);
            currentY += h + Spacing;

            child.Arrange(rect);
        }
    }

    protected void DrawHorizontal(Size finalSize)
    {
        double currentX = Padding.Left;
        double currentY = Padding.Top;
        int visChildrens = Children.Count(x => x.IsVisible);

        // find FILLs
        double availableWidth = finalSize.Width - (Padding.Right + Padding.Left);
        double availableHeight = finalSize.Height - (Padding.Top + Padding.Bottom);
        if (visChildrens > 1)
            availableWidth -= (visChildrens - 1) * Spacing;

        int countFills = Children.Count(x => x.HorizontalAlignment == HorizontalAlignment.Stretch && x.IsVisible);
        double freeSize = availableWidth - Children.Sum(x => x.HorizontalAlignment != HorizontalAlignment.Stretch ? x.DesiredSize.Width : 0);
        double fillSize = freeSize / countFills;


        // draws
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
                continue;

            Rect rect;
            double y = currentY;
            double h;

            switch (child.VerticalAlignment)
            {
                case VerticalAlignment.Stretch:
                    h = availableWidth;
                    break;
                case VerticalAlignment.Top:
                    h = child.DesiredSize.Height;
                    break;
                case VerticalAlignment.Center:
                    h = child.DesiredSize.Height;
                    y = (finalSize.Height / 2) - (h / 2);
                    break;
                case VerticalAlignment.Bottom:
                    h = child.DesiredSize.Height;
                    y = availableWidth - h;
                    break;
                default:
                    throw new NotSupportedException();
            }

            double w = child.HorizontalAlignment == HorizontalAlignment.Stretch
                ? fillSize
                : child.DesiredSize.Width;

            rect = new Rect(currentX, y, w, h);
            currentX += w + Spacing;

            child.Arrange(rect);
        }
    }
}
