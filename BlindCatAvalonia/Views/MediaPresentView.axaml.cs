using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using BlindCatAvalonia.MediaPlayers;
using BlindCatAvalonia.SDcontrols;
using BlindCatAvalonia.Tools;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BlindCatAvalonia;

public partial class MediaPresentView : Grid, MediaPresentVm.IPresentedView
{
    private IMediaBase? _mediaBase;
    private TimeSpan videoDuration;
    private MediaPresentVm _vm = null!;

    public MediaPresentView()
    {
        CommandCopyImage = new Cmd(CopyImage);
        CommandCopyPath = new Cmd(CopyPath);
        CommandExport = new Cmd(ExportFile);
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            var file = new LocalFile
            {
                FilePath = @"C:\data\test.jpg",
            };
            var vm = new MediaPresentVm(new MediaPresentVm.Key
            {
                SourceDir = new LocalDir
                {
                    DirPath = @"C:\data",
                },
                SourceFile = file,
            }, new DesignStorageService(), null, null, null)
            {
                NavigationService = null,
                ViewModelResolver = null,
                ViewPlatforms = null,
            };
            DataContext = vm;
            _ = SetSource(file, MediaPresentVm.ResolveFormat(file.FilePath), CancellationToken.None);
        }

    }

    public ICommand CommandCopyImage { get; private set; }
    public ICommand CommandCopyPath { get; private set; }
    public ICommand CommandExport { get; private set; }

    public IMediaBase? MediaBase
    {
        get => _mediaBase;
        private set
        {
            var old = _mediaBase;
            if (old != null)
            {
                old.ErrorReceived -= OnError;
                old.ZoomChanged -= OnZoomChanged;
                old.MetaReceived -= OnMetaReceived;
            }
            if (old is IMediaPlayer oldPlayer)
            {
                oldPlayer.PlayingProgressChanged -= VideoPlayerToolkit_PlayingProgress;
                oldPlayer.StateChanged -= VideoPlayerToolkit_PlayPauseChanged;
            }

            if (value is IMediaPlayer newPlayer)
            {
                newPlayer.PlayingProgressChanged += VideoPlayerToolkit_PlayingProgress;
                newPlayer.StateChanged += VideoPlayerToolkit_PlayPauseChanged;
            }
            if (value != null)
            {
                value.ZoomChanged += OnZoomChanged;
                value.ErrorReceived += OnError;
                value.MetaReceived += OnMetaReceived;
            }
            _mediaBase = value;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        slider.ValueChanged += Slider_ValueChanged;
        slider.IsSliderThumbPressed += Slider_Pressed;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        slider.ValueChanged -= Slider_ValueChanged;
        slider.IsSliderThumbPressed -= Slider_Pressed;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MediaPresentVm vm)
        {
            _vm = vm;

            var col = new Avalonia.Collections.AvaloniaList<SDcontrols.MenuItem>();
            foreach (var item in vm.TopButtons)
            {
                col.Add(new SDcontrols.MenuItem
                {
                    Command = item.Command,
                    Text = item.Name,
                });
            }
            Scaffolt.SetMenuItems(this, col);
        }
    }

    private async void CopyImage()
    {
        if (_vm.IsLoading)
            return;

        using var busy = _vm.Loading();
        var platform = App.ServiceProvider.GetRequiredService<IViewPlatforms>();
        var bmp = imageSkia.UnsafeBitmap;
        if (bmp == null)
            return;

        await platform.Clipboard.SetImage(bmp);
    }

    private void CopyPath()
    {
        if (_vm.IsLoading)
            return;

        var platform = App.ServiceProvider.GetRequiredService<IViewPlatforms>();
        platform.Clipboard.SetText(_vm.CurrentFile.FilePath);
    }

    private async void ExportFile()
    {
        if (_vm.IsLoading)
            return;

        var file = _vm.CurrentFile;
        var storageSrv = App.ServiceProvider.GetRequiredService<IStorageService>();
        string? password = storageSrv.CurrentStorage.Password;

        using var busy = _vm.Loading();
        var crypto = App.ServiceProvider.GetRequiredService<ICrypto>();
        using var dec = await crypto.DecryptFile(file.FilePath, password, CancellationToken.None);
        if (dec.IsFault)
        {
            await _vm.HandleError(dec);
            return;
        }

        var vp = App.ServiceProvider.GetRequiredService<IViewPlatforms>();
        string? saveto = await vp.SaveTo(file.FileName, null);
        if (saveto == null)
            return;

        using var saveFile = File.OpenWrite(saveto);
        await dec.Result.CopyToAsync(saveFile);
        await _vm.ShowMessage("Success", "File export successful", "OK");
    }

    private async void Slider_Pressed(object? invoker, bool isPressed)
    {
        if (MediaBase is not IMediaPlayer mp)
            return;

        if (isPressed)
        {
            mp.Pause();
        }
        else
        {
            using var loading = _vm.Loading(MediaPresentVm.playerLoading);
            var cancel = _vm.RefreshCancelation();
            await mp.SeekTo(slider.Value, cancel);
            mp.Play();
        }
    }

    private void Slider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        //if (isSliderThumbDrugged)
        //{
        //    Debug.WriteLine("USER CHANGED");
        //}
        //else
        //{
        //    Debug.WriteLine("VIDEO PROGRESS");
        //}
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (MediaBase == null)
            return;

        // up
        if (e.Delta.Y > 0)
        {
            MediaBase.Zoom += 0.1;
        }
        // down
        else if (e.Delta.Y < 0)
        {
            MediaBase.Zoom -= 0.1;
        }
    }

    private bool isPressed;
    private Avalonia.Point lastPoint;
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetCurrentPoint(this);
        if (!p.Properties.IsLeftButtonPressed)
            return;

        isPressed = true;
        var point = e.GetPosition(this);
        lastPoint = point;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
            isPressed = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!isPressed)
            return;

        if (MediaBase == null)
            return;

        var point = e.GetPosition(this);
        double deltaX = lastPoint.X - point.X;
        double deltaY = lastPoint.Y - point.Y;
        float x = MediaBase.PositionOffset.X - (float)deltaX;
        float y = MediaBase.PositionOffset.Y - (float)deltaY;
        MediaBase.PositionOffset = new PointF(x, y);
        lastPoint = point;
    }

    private void OnMetaReceived(object? sender, FileMetaData[] e)
    {
        _vm.OnMetaReceived(sender, e);
    }

    private void OnZoomChanged(object? sender, double e)
    {
        int value = (int)(e * 100);
        labelZoom.Text = $"{value}%";
    }

    private void OnError(object? sender, string? textError)
    {
        bool show = textError != null;
        errorLabel.Text = textError;
        errorPanel.IsVisible = show;
    }

    private void VideoPlayerToolkit_PlayPauseChanged(object? sender, MediaPlayerStates e)
    {
        switch (e)
        {
            case MediaPlayerStates.Playing:
                pathPause.IsVisible = true;
                pathPlay.IsVisible = false;
                break;
            case MediaPlayerStates.None:
            case MediaPlayerStates.Pause:
            case MediaPlayerStates.Stopped:
            case MediaPlayerStates.ReadyToPlaying:
            default:
                pathPause.IsVisible = false;
                pathPlay.IsVisible = true;
                break;
        }
    }

    private void VideoPlayerToolkit_PlayingProgress(object? sender, double percentOfPlaying)
    {
        if (!slider.IsSliderThumbDrugged)
        {
            Dispatcher.UIThread.Post(() =>
            {
                slider.Value = percentOfPlaying;

                string text = ToPlayTimeText(videoDuration * percentOfPlaying);
                textrunPos.Text = text;
            });
        }
    }

    public void Dispose()
    {
    }

    public void Pause()
    {
        if (MediaBase is IMediaPlayer mp)
        {
            mp?.Pause();
        }
    }

    public async Task SetSource(object source, MediaFormats format, CancellationToken cancel)
    {
        var old = MediaBase;
        IMediaBase? newest;
        switch (format)
        {
            case MediaFormats.Unknown:
                newest = null;
                break;
            default:
                if (format.IsVideo())
                {
                    newest = videoPlayerSkia;
                }
                else if (format.IsPicture())
                {
                    newest = imageSkia;
                }
                else
                {
                    throw new NotImplementedException();
                }
                break;
        }

        old?.Reset();
        errorPanel.IsVisible = false;

        if (old != newest)
        {
            if (old is Control vOld)
                vOld.IsVisible = false;

            if (newest is Control vNew)
                vNew.IsVisible = true;

            bool isNewVideo = format.IsVideo();

            slider.IsVisible = isNewVideo;
            textBlockTimes.IsVisible = isNewVideo;
            buttonPlayPause.IsVisible = isNewVideo;
            buttonVolume.IsVisible = isNewVideo;
        }

        MediaBase = newest;

        try
        {
            if (newest != null)
            {
                switch (source)
                {
                    case LocalFile stringSource:
                        await newest.SetSourceLocal(stringSource.FilePath, cancel);
                        break;
                    case StorageFile storageFileSource:
                        await newest.SetSourceStorage(storageFileSource, cancel);
                        break;
                    case ISourceFile sourcef:
                        await newest.SetSourceLocal(sourcef.FilePath, cancel);
                        break;
                    default:
                        Debugger.Break();
                        throw new NotImplementedException();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            OnError(this, ex.ToString());
        }

        if (newest is IMediaPlayer mp)
        {
            videoDuration = mp.Duration;
            textrunPos.Text = ToPlayTimeText(TimeSpan.Zero);
            textrunDuration.Text = ToPlayTimeText(videoDuration);
        }
    }

    public void Stop()
    {
        MediaBase?.Reset();
    }

    public void ZoomMinus()
    {
        throw new System.NotImplementedException();
    }

    public void ZoomPlus()
    {
        throw new System.NotImplementedException();
    }

    public FileMetaData[]? GetMeta()
    {
        return MediaBase?.GetMeta();
    }

    private static string ToPlayTimeText(TimeSpan time)
    {
        var sb = new StringBuilder();

        // hours
        int hours = (int)time.TotalHours;
        if (hours >= 1)
        {
            sb.Append(hours);
            sb.Append(':');
        }

        // mins
        if (time.Minutes < 10)
            sb.Append('0');
        sb.Append(time.Minutes);
        sb.Append(':');

        if (time.Seconds < 10)
            sb.Append('0');
        sb.Append(time.Seconds);

        return sb.ToString();
    }
}