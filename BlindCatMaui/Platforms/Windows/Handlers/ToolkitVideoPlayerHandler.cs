using BlindCatCore.ViewModels;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using BlindCatMaui.SDControls;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace BlindCatMaui.Platforms.Windows.Handlers;

public class ToolkitVideoPlayerHandler : MediaElementHandler
{
    public MediaManager2 MediaMan => (MediaManager2)this.mediaManager;

    protected override MauiMediaElement CreatePlatformView()
    {
        if (mediaManager == null)
        {
            mediaManager = new MediaManager2(base.MauiContext ?? throw new NullReferenceException(), base.VirtualView, Dispatcher.GetForCurrentThread() ?? throw new InvalidOperationException("IDispatcher cannot be null"));
        }

        return new MauiMediaElement(mediaManager.CreatePlatformView());
        //return base.CreatePlatformView();
    }
}

public class MediaManager2 : MediaManager
{
    public MediaManager2(IMauiContext context, IMediaElement mediaElement, IDispatcher dispatcher) : base(context, mediaElement, dispatcher)
    {
    }

    protected override void PlatformUpdateSource()
    {
        base.PlatformUpdateSource();
        if (MediaElement.Source is SourceStreamWrapper sts)
        {
            var winuiStream = sts.Stream.AsRandomAccessStream();

            string mime = MediaPresentVm.ResolveMimeType(sts.MediaFormat);
            Player.Source = global::Windows.Media.Core.MediaSource.CreateFromStream(winuiStream, mime);
        }
    }
}