using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.ViewModels;
using BlindCatMaui.Core;

namespace BlindCatMaui.Views;

public partial class MediaPresentView : MediaPresentVm.IPresentedView, IDisposable
{
    private double _lastX;
    private double _lastY;
    private bool _isSliderPlayingProgressDragged;
    private CancellationTokenSource _seekCancels = new();
    private IMediaBase? _mediaBase;

    public MediaPresentView()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        MediaBase?.Reset();
    }

    #region props
    public IMediaBase? MediaBase
    {
        get => _mediaBase;
        private set
        {
            var old = _mediaBase;
            if (old != null)
            {
                old.ZoomChanged -= Img_ZoomChanged;
            }
            if (old is IMediaPlayer mp)
            {
                mp.PlayingProgressChanged -= VideoPlayerToolkit_PlayingProgress;
                mp.StateChanged -= VideoPlayerToolkit_PlayPauseChanged;
            }

            if (value is IMediaPlayer nmp)
            {
                nmp.PlayingProgressChanged += VideoPlayerToolkit_PlayingProgress;
                nmp.StateChanged += VideoPlayerToolkit_PlayPauseChanged;
            }
            if (value != null)
            {
                value.ZoomChanged += Img_ZoomChanged;
            }
            _mediaBase = value;
        }
    }

    public int Zoom
    {
        get
        {
            double z = MediaBase?.Zoom ?? 0.0;
            int res = (int)(z * 100.0);
            return res;
        }
    }
    #endregion props

    public async Task SetSource(object source, MediaFormats handler, CancellationToken cancel)
    {
        var old = MediaBase;
        IMediaBase? newest;

        if (handler.IsVideo())
        {
            newest = skiaVideoPlayer;
        }
        else if (handler.IsPicture())
        {
            if (handler == MediaFormats.Gif)
                newest = imgMaui;
            else
                newest = img;
        }
        else
        {
            throw new NotImplementedException();
        }

        old?.Reset();

        if (old != newest)
        {
            if (old is View vOld)
                vOld.IsVisible = false;

            if (newest is View vNew)
                vNew.IsVisible = true;

            bool isNewVideo = handler.IsVideo();
            if (isNewVideo)
            {
                this.BackgroundColor = Colors.Black;
                videoControllers.IsVisible = true;
            }
            else
            {
                this.BackgroundColor = Color.FromArgb("#222");
                videoControllers.IsVisible = false;
            }
        }

        MediaBase = newest;

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
                    throw new NotImplementedException();
            }
        }
    }

    public void Pause()
    {
        if (MediaBase is IMediaPlayer mp)
        {
            mp.Pause();
        }
    }

    public void Stop()
    {
        MediaBase?.Reset();
    }

    public void ZoomMinus()
    {
        if (MediaBase != null)
        {
            MediaBase.Zoom -= 0.05;
        }
    }

    public void ZoomPlus()
    {
        if (MediaBase != null)
        {
            MediaBase.Zoom += 0.05;
        }
    }

    private void OnPlayingPositionChanged(double progress)
    {
        this.Dispatcher.Dispatch(() =>
        {
            if (MediaBase is IMediaPlayer mp)
            {
                var res = mp.Duration * progress;
                string pos = res.ToString("m\\:ss");
                labelPlayingPosition.Text = res.ToString("m\\:ss");
            }
        });
    }

    private void OnDurationChanged(TimeSpan duration)
    {
        if (MediaBase is IMediaPlayer mp)
        {
            labelDuration.Text = duration.ToString("m\\:ss");
        }
    }

    #region handlers
    private void Img_ZoomChanged(object? sender, double e)
    {
        OnPropertyChanged(nameof(Zoom));
    }

    private void VideoPlayerToolkit_PlayingProgress(object? sender, double e)
    {
        this.Dispatcher.Dispatch(() =>
        {
            if (_isSliderPlayingProgressDragged)
                return;

            videoProgressSlider.Value = e;
            OnPlayingPositionChanged(e);
        });
    }

    private IDispatcherTimer? _testTimer;
    int secs;
    private void OnTestTimer(object? invoker, EventArgs e)
    {
        secs++;;
        string pos = TimeSpan.FromSeconds(secs).ToString("m\\:ss");
        testVideoPos.Text = pos;
    }

    private void VideoPlayerToolkit_PlayPauseChanged(object? sender, MediaPlayerStates e)
    {
        Dispatcher.Dispatch(() =>
        {
            if (buttonPlayPause.Content is not Label l)
                return;

            switch (e)
            {
                case MediaPlayerStates.Playing:
                    l.Text = "Pause";
                    if (_testTimer == null)
                    {
                        _testTimer = this.Dispatcher.CreateTimer();
                        _testTimer.Interval = TimeSpan.FromSeconds(1);
                        _testTimer.IsRepeating = true;
                        _testTimer.Tick += OnTestTimer;
                    }
                    _testTimer.Start();
                    break;
                case MediaPlayerStates.Pause:
                    l.Text = "Play";
                    _testTimer?.Stop();
                    break;
                case MediaPlayerStates.ReadyToPlaying:
                    if (MediaBase is IMediaPlayer mp)
                    {
                        OnDurationChanged(mp.Duration);
                    }
                    break;
                default:
                    l.Text = "Stop";
                    break;
            }
        });
    }

    private void PanGestureRecognizer_PanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (MediaBase == null)
            return;

        double currentX = MediaBase.PositionXPercent * Width;
        double currentY = MediaBase.PositionYPercent * Height;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lastX = 0;
                _lastY = 0;
                break;
            case GestureStatus.Running:
                double difX = e.TotalX - _lastX;
                double difY = e.TotalY - _lastY;
                double nx = currentX + difX;
                double ny = currentY + difY;

                //Debug.WriteLine($"totalX {e.TotalX}\ntotalY{e.TotalY}");

                double imagePosPercentX = nx / Width;
                double imagePosPercentY = ny / Height;
                MediaBase.SetPercentPosition(imagePosPercentX, imagePosPercentY);
                _lastX = e.TotalX;
                _lastY = e.TotalY;
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                break;
            default:
                break;
        }
    }

    private void PinchGestureRecognizer_PinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
        double dif = 1 - e.Scale;

        if (MediaBase != null)
            MediaBase.Zoom -= dif;
    }

    private bool rememberIsPlaying;
    private void videoProgressSlider_DragStarted(object sender, EventArgs e)
    {
        _isSliderPlayingProgressDragged = true;
        if (MediaBase is IMediaPlayer mp)
        {
            rememberIsPlaying = mp.State == MediaPlayerStates.Playing;
            mp.Pause();
        }
    }

    private void videoProgressSlider_DragCompleted(object sender, EventArgs e)
    {
        _isSliderPlayingProgressDragged = false;
        if (MediaBase is IMediaPlayer mp)
        {
            double v = videoProgressSlider.Value;
            OnPlayingPositionChanged(v);
            _seekCancels.Cancel();
            _seekCancels = new();
            mp.SeekTo(v, _seekCancels.Token);

            if (rememberIsPlaying)
                mp.Play();

            rememberIsPlaying = false;
        }
    }

    private void videoProgressSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (!_isSliderPlayingProgressDragged)
            return;

        if (MediaBase is IMediaPlayer mp)
        {
            OnPlayingPositionChanged(e.NewValue);
            //_seekCancels.Cancel();
            //_seekCancels = new();
            //mp.SeekTo(e.NewValue, _seekCancels.Token);
        }
    }

    private void videoProgressSlider_SliderJumped(object sender, double e)
    {
        //if (MediaBase is IMediaPlayer mp)
        //{
        //    OnPlayingPositionChanged(e);
        //    _seekCancels.Cancel();
        //    _seekCancels = new();
        //    mp.SeekTo(e, _seekCancels.Token);
        //}
    }
    #endregion handlers

    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        if (MediaBase is IMediaPlayer mp)
        {
            if (videoControllers.AnimationIsRunning("Hide"))
            {
                await videoControllers.FadeTos(1, 200, "Show");
            }
            else if (videoControllers.AnimationIsRunning("Show"))
            {
                if (await videoControllers.FadeTos(0, 200, "Hide"))
                    videoControllers.IsVisible = false;
            }
            else
            {
                if (videoControllers.IsVisible)
                {
                    if (await videoControllers.FadeTos(0, 200, "Hide"))
                        videoControllers.IsVisible = false;
                }
                else
                {
                    videoControllers.IsVisible = true;
                    await videoControllers.FadeTos(1, 200, "Show");
                }
            }
        }
    }
}