using Avalonia.Controls.Skia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using BlindCatAvalonia.Core;
using BlindCatCore.Core;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jfif;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.MediaPlayers;

public class ImageSkia : SKBitmapControlExt, IMediaBase
{
    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<string?>? ErrorReceived;
    public event EventHandler<FileMetaData[]?>? MetaReceived;

    public double Zoom
    {
        get => RenderScale;
        set
        {
            if (value <= 0.2)
                value = 0.2;
            else if (value >= 5.0)
                value = 5.0;

            ForceScale = value;
        }
    }
    public double PositionXPercent => 0.5;
    public double PositionYPercent => 0.5;
    public PointF PositionOffset
    {
        get => Offset;
        set => Offset = value;
    }

    public void InvalidateSurface()
    {
        this.InvalidateVisual();
    }

    protected override void OnScaleChanged(double scale)
    {
        base.OnScaleChanged(scale);
        Dispatcher.UIThread.Post(() =>
        {
            ZoomChanged?.Invoke(this, scale);
        });
    }

    public void Reset()
    {
        Bitmap = null;
        ResetOffsetAndScale(false);
    }

    public void SetPercentPosition(double imagePosPercentX, double imagePosPercentY)
    {
        throw new NotImplementedException();
    }

    private string? source;

    public async Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        var sizeMeta = MakeMeta(filePath, null, true, false, false);
        MetaReceived?.Invoke(this, sizeMeta.Meta);

        var bmp = await TaskExt.Run(() =>
        {
            return SKBitmap.Decode(filePath);
        }, cancel);

        if (bmp == null)
            return;

        source = filePath;
        var m = MakeMeta(filePath, bmp, false, true, true);
        MetaReceived?.Invoke(this, m.Meta);

        if (m.RotatedImage != null)
            Bitmap = m.RotatedImage;
        else
            Bitmap = bmp;
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public async Task SetSourceStorage(StorageFile file, CancellationToken cancel)
    {
        using var decode = await this.DI<ICrypto>().DecryptFile(file.FilePath, file.Storage.Password, cancel);
        if (decode.IsCanceled)
            return;

        if (decode.IsFault)
        {
            ErrorReceived?.Invoke(this, decode.Description);
            return;
        }

        var resBitmap = await TaskExt.Run(() =>
        {
            byte[] bin = decode.Result.ToArray();
            return SKBitmap.Decode(bin);
        }, cancel);

        if (cancel.IsCancellationRequested)
            return;

        if (resBitmap == null)
        {
            string msg = $"Fail decode binary for \"{file.Name}\" [SKBitmap.Decode]";
            ErrorReceived?.Invoke(this, msg);
            Debug.WriteLine(msg);
            Bitmap = null;
            return;
        }

        Bitmap = resBitmap;
    }

    public static MakeMetaResult MakeMeta(string filePath, SKBitmap? bmp, bool makeFileSize, bool makeImgInfo, bool makeRotationTransform)
    {
        SKBitmap? rotated = null;
        var meta = new FileMetaData
        {
            GroupName = "Meta",
            MetaItems = [],
        };
        var result = new List<FileMetaData>
        {
            meta,
        };

        if (makeFileSize)
        {
            var fileInfo = new FileInfo(filePath);
            meta.MetaItems.Insert(0, new FileMetaItem
            {
                Meta = "File size",
                Value = fileInfo.Length.ToString(),
            });
        }

        if (makeImgInfo)
        {
            meta.MetaItems.Add(new()
            {
                Meta = "Matrix",
                Value = $"{bmp.Info.Width} x {bmp.Info.Height}",
            });
            meta.MetaItems.Add(new()
            {
                Meta = "Bit depth",
                Value = (bmp.BytesPerPixel * 8).ToString(),
            });

            var directoris = ImageMetadataReader.ReadMetadata(filePath);
            int dpiX = 0;
            int dpiY = 0;
            int orientation = 1;
            bool hasdpiX = directoris.FirstOrDefault(x => x.Name == "JFIF")?.TryGetInt32(8, out dpiX) ?? false;
            bool hasdpiY = directoris.FirstOrDefault(x => x.Name == "JFIF")?.TryGetInt32(8, out dpiY) ?? false;
            if (hasdpiX && hasdpiY)
                meta.MetaItems.Add(new FileMetaItem
                {
                    Meta = "DPI",
                    Value = Math.Max(dpiX, dpiY).ToString(),
                });


            foreach (var directory in directoris)
            {
                var dirGroup = new System.Collections.ObjectModel.ObservableCollection<FileMetaItem>();
                result.Add(new FileMetaData
                {
                    GroupName = directory.Name,
                    MetaItems = dirGroup,
                });

                if (directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationParse))
                {
                    orientation = orientationParse;
                }

                if (directory.TryGetInt32(ExifDirectoryBase.TagXResolution, out int xResolutionParse))
                {
                    dpiX = xResolutionParse;
                }

                if (directory.TryGetInt32(ExifDirectoryBase.TagYResolution, out int resolY))
                {
                    dpiY = resolY;
                }

                foreach (var tag in directory.Tags)
                {
                    Debug.WriteLine($"{directory.Name} - {tag.Name}({tag.Type}) = {tag.Description}");

                    if (tag.Description != null)
                        dirGroup.Add(new FileMetaItem
                        {
                            Meta = tag.Name,
                            Value = tag.Description,
                        });
                }
            }

            if (makeRotationTransform)
            {
                rotated = ApplyExifOrientation(bmp, orientation);
            }
        }

        return new MakeMetaResult
        {
            Meta = result.ToArray(),
            RotatedImage = rotated,
        };
    }

    public FileMetaData[]? GetMeta()
    {
        if (source == null || UnsafeBitmap == null)
            return null;

        return MakeMeta(source, UnsafeBitmap, true, true, false).Meta;
    }

    public static SKBitmap ApplyExifOrientation(SKBitmap bitmap, int orientation)
    {
        SKMatrix matrix;
        int newWidth = bitmap.Width;
        int newHeight = bitmap.Height;
        int x = 0;
        int y = 0;

        switch (orientation)
        {
            // Отразить по горизонтали
            case 2: 
                matrix = SKMatrix.CreateScale(-1, 1, bitmap.Width / 2f, bitmap.Height / 2f);
                break;
            // Повернуть на 180 градусов
            case 3: 
                matrix = SKMatrix.CreateRotationDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                break;
            // Отразить по вертикали
            case 4: 
                matrix = SKMatrix.CreateScale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
                break;
            // Повернуть на 90 градусов против часовой стрелки и отразить по вертикали
            case 5: 
                matrix = SKMatrix.CreateRotationDegrees(90, bitmap.Width / 2f, bitmap.Height / 2f);
                matrix = matrix.PreConcat(SKMatrix.CreateScale(1, -1));
                newWidth = bitmap.Height;
                newHeight = bitmap.Width;
                x = (bitmap.Width - bitmap.Height) / 2;
                y = -(bitmap.Height + x);
                break;
            // Повернуть на 90 градусов по часовой стрелке
            case 6: 
                matrix = SKMatrix.CreateRotationDegrees(90, bitmap.Width / 2f, bitmap.Height / 2f);
                newWidth = bitmap.Height;
                newHeight = bitmap.Width;
                x = (bitmap.Width - bitmap.Height) / 2;
                y = x;
                break;
            // Повернуть на 90 градусов по часовой стрелке и отразить по горизонтали
            case 7: 
                matrix = SKMatrix.CreateRotationDegrees(90, bitmap.Width / 2f, bitmap.Height / 2f);
                matrix = matrix.PreConcat(SKMatrix.CreateScale(-1, 1));
                newWidth = bitmap.Height;
                newHeight = bitmap.Width;
                y = (bitmap.Width - bitmap.Height) / 2;
                x = -(bitmap.Width + y);
                break;
            // Повернуть на 90 градусов против часовой стрелки
            case 8: 
                matrix = SKMatrix.CreateRotationDegrees(-90, bitmap.Width / 2f, bitmap.Height / 2f);
                newWidth = bitmap.Height;
                newHeight = bitmap.Width;
                x = (bitmap.Height - bitmap.Width) / 2;
                y = x;
                break;
            // Ориентация по умолчанию (1)
            default: 
                return bitmap;
        }

        var rotatedBitmap = new SKBitmap(newWidth, newHeight);
        using (var canvas = new SKCanvas(rotatedBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(bitmap, x, y);
        }

        return rotatedBitmap;
    }

    public class MakeMetaResult
    {
        public required FileMetaData[] Meta { get; set; }
        public SKBitmap? RotatedImage { get; set; }
    }
}