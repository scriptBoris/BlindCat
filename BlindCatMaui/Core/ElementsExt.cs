using BlindCatCore.Core;
using BlindCatCore.Extensions;

namespace BlindCatMaui.Core;

internal static class ElementsExt
{
    public static T DiFetch<T>(this Element element) where T : notnull
    {
        return App.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Уменьшение, 1.0 - без изменений.
    /// </summary>
    public static Rect Shrink(this Rect rect, double mod)
    {
        double scale = 1 / mod;

        return new Rect
        {
            X = rect.X * scale,
            Y = rect.Y * scale,
            Width = rect.Width * scale,
            Height = rect.Height * scale,
        };
        //return ShrinkPrivate(rect, mod);
    }

    private static Rect ShrinkPrivate(Rect rect, double shrink)
    {
        if (shrink == 1)
            return rect;

        // Вычисляем новые размеры
        double newWidth = rect.Width / shrink;
        double newHeight = rect.Height / shrink;

        // Вычисляем новое положение, чтобы центр оставался на том же месте
        double newX = rect.X + (rect.Width - newWidth) / 2;
        double newY = rect.Y + (rect.Height - newHeight) / 2;

        // Создаем и возвращаем новый Rect с новыми размерами и положением
        return new Rect(newX, newY, newWidth, newHeight);
    }

    public static T WithChildrens<T>(this T view, params View?[] childs) where T : View
    {
        if (view is Layout l)
        {
            foreach (var child in childs)
            {
                if (child == null) 
                    continue;

                l.Children.Add(child);
            }
        }
        return view;
    }

    public static BaseVm? BindingContextVm(this Element element)
    {
        return element.BindingContext as BaseVm;
    }

    public static Page GetParentPage(this View element)
    {
        Element parent = element.Parent;
        while (parent != null)
        {
            if (parent is Page p)
                return p;

            parent = parent.Parent;
        }
        return null;
    }

    public static async Task<IElementHandler?> AwaitHandler(this Element view, CancellationToken? cancel = null)
    {
        if (view.Handler != null)
            return view.Handler;

        var tsc = new TaskCompletionSource<IElementHandler>();
        void eventDelegate(object? sender, EventArgs e)
        {
            tsc.TrySetResult(view.Handler!);
        }

        view.HandlerChanged += eventDelegate;
        var handler = await tsc.AwaitWithCancelation(cancel ?? CancellationToken.None);
        view.HandlerChanged -= eventDelegate;

        return handler;
    }

}