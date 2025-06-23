using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.ViewModels;
using ScaffoldLib.Maui.Core;

namespace BlindCatMauiMobile.Views;

public partial class MediaPresentView : MediaPresentVm.IPresentedView, IRemovedFromNavigation
{
    public MediaPresentView()
    {
        InitializeComponent();
    }

    public IMediaBase? MediaBase { get; private set; }
    
    public Task SetSource(object source, MediaFormats format, CancellationToken cancel)
    {
        bool isVideo = format.IsVideo();
        var oldMediaBase = MediaBase;

        if (isVideo)
        {
            MediaBase = VideoPlayer;
        }
        else
        {
            throw new NotImplementedException();
        }

        if (oldMediaBase != MediaBase)
        {
            if (oldMediaBase is View oldView)
                oldView.IsVisible = false;
            
            if (MediaBase is View newView)
                newView.IsVisible = true;
        }

        switch (source)
        {
            case LocalFile locFile:
                return MediaBase.SetSourceLocal(locFile.FilePath, cancel);
            case string localFile:
                return MediaBase.SetSourceLocal(localFile, cancel);
            default:
                throw new NotImplementedException();
        }
    }

    public void Pause()
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }

    public void ZoomMinus()
    {
        throw new NotImplementedException();
    }

    public void ZoomPlus()
    {
        throw new NotImplementedException();
    }

    public FileMetaData[]? GetMeta()
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        VideoPlayer.Dispose();
    }

    public void OnRemovedFromNavigation()
    {
        Dispose();
    }
}