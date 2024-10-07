using SkiaSharp;

namespace BlindCatMaui.Core;

public class SKDrawTextArgs
{
    public required string Text { get; set; }
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor? OutlineColor { get; set; }
    public float TextSize { get; set; } = 12.0f;
    public float OutlineWidth { get; set; }
    public TextAlignment VerticalAlignment { get; set; }
    public TextAlignment HorizontalAlignment { get; set; }
}

public static class SkiaExt
{
    public static void DrawTextWithOutline(SKBitmap bitmap, SKDrawTextArgs args)
    {
        using var canvas = new SKCanvas(bitmap);

        // Настраиваем параметры обводки текста
        SKPaint? outlinePaint = null;
        bool useOutline = args.OutlineColor != null && args.OutlineWidth > 0;
        if (useOutline)
        {
            outlinePaint = new SKPaint
            {
                TextSize = args.TextSize,
                IsAntialias = true,
                Color = args.OutlineColor!.Value,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = args.OutlineWidth,
            };
        }

        // Настраиваем параметры текста
        using var textPaint = new SKPaint
        {
            TextSize = args.TextSize,
            IsAntialias = true,
            Color = args.TextColor,
            Style = SKPaintStyle.Fill,
        };

        // Определяем размеры текста
        var textBounds = new SKRect();
        textPaint.MeasureText(args.Text, ref textBounds);
        float x;

        switch (args.HorizontalAlignment)
        {
            case TextAlignment.Start:
                x = 0;
                break;
            case TextAlignment.Center:
                x = (bitmap.Width - textBounds.Width) / 2 - textBounds.Left;
                //x = textBounds.Width;
                break;
            case TextAlignment.End:
                x = bitmap.Width - textBounds.Width - 10; // 10 пикселей отступа от правого края
                break;
            default:
                throw new NotImplementedException();
        }

        float y;
        switch (args.VerticalAlignment)
        {
            case TextAlignment.Start:
                y = textPaint.TextSize;
                break;
            case TextAlignment.Center:
                y = (bitmap.Height - textBounds.Height) / 2 - textBounds.Top;
                break;
            case TextAlignment.End:
                y = bitmap.Height - textBounds.Height + textPaint.TextSize - 10; // текст будет прижат к нижнему краю
                break;
            default:
                throw new NotImplementedException();
        }

        // Вычисляем позицию текста снизу справа
        //float x = bitmap.Width - textBounds.Width - 10; // 10 пикселей отступа от правого края
        //float y = bitmap.Height - textBounds.Height + textPaint.TextSize - 10; // текст будет прижат к нижнему краю

        // Рисуем обводку текста на канвасе
        if (useOutline)
        {
            canvas.DrawText(args.Text, x, y, outlinePaint);
        }

        // Рисуем сам текст на канвасе
        canvas.DrawText(args.Text, x, y, textPaint);

        outlinePaint?.Dispose();
    }

    public static SKBitmap ResizeBitmap(SKBitmap srcBitmap, int newWidth, int newHeight)
    {
        // Создаем новый SKBitmap с заданными размерами
        var dstBitmap = new SKBitmap(newWidth, newHeight);

        // Создаем SKCanvas для нового SKBitmap
        using (var canvas = new SKCanvas(dstBitmap))
        {
            // Настраиваем качество масштабирования
            var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High
            };

            // Рисуем исходное изображение на новом канвасе с масштабированием
            canvas.DrawBitmap(srcBitmap, SKRect.Create(srcBitmap.Width, srcBitmap.Height),
                              SKRect.Create(newWidth, newHeight), paint);
        }

        return dstBitmap;
    }
}
