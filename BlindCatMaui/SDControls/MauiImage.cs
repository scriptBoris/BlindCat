using BlindCatCore.Core;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatMaui.Core;
using System.Diagnostics;

namespace BlindCatMaui.SDControls;

public class MauiImage : Image, IMediaBase
{
    private double viewPortWidth;
    private double viewPortHeight;

    public event EventHandler<double>? ZoomChanged;

    #region props
    public double Zoom
    {
        get => Scale;
        set
        {
            Scale = value;
            ZoomChanged?.Invoke(this, value);
        }
    }

    public double PositionXPercent { get; private set; } = 0.5;
    public double PositionYPercent { get; private set; } = 0.5;
    #endregion props

    public void InvalidateSurface()
    {
    }

    public void SetPercentPosition(double imagePosPercentX, double imagePosPercentY)
    {
        PositionXPercent = imagePosPercentX;
        PositionYPercent = imagePosPercentY;

        UpdatePos(viewPortWidth, viewPortHeight, Width, Height);
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        viewPortWidth = widthConstraint;
        viewPortHeight = heightConstraint;

        var res = base.MeasureOverride(widthConstraint, heightConstraint);
        if (!res.IsZero)
        {
            UpdatePos(widthConstraint, heightConstraint, res.Width, res.Height);
        }
        return res;
    }

    private void UpdatePos(double vpWidth, double vpHeight, double imgWidth, double imgHeight)
    {
        double centerX = vpWidth * PositionXPercent - (imgWidth / 2);
        double centerY = vpHeight * PositionYPercent - (imgHeight / 2);
        TranslationX = centerX;
        TranslationY = centerY;
    }

    public Task SetSourceLocal(string filePath, CancellationToken cancel)
    {
        Source = filePath;
        return Task.CompletedTask;
    }

    public Task SetSourceRemote(string url, CancellationToken cancel)
    {
        throw new NotImplementedException();
    }

    public void Reset()
    {
        Source = null;
        PositionXPercent = 0.5;
        PositionYPercent = 0.5;
        Zoom = 1.0;
    }

    public async Task SetSourceStorage(StorageFile file, CancellationToken cancel)
    {
        var decode = await this.DiFetch<ICrypto>().DecryptFile(file.FilePath, file.Storage.Password, cancel);
        if (decode.IsCanceled)
            return;

        if (decode.IsFault)
        {
            Debugger.Break();
            Source = null;
            return;
        }

        Source = ImageSource.FromStream(() => decode.Result);
    }
}
