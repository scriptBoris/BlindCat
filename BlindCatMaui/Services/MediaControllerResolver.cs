using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using BlindCatMaui.Controllers;

namespace BlindCatMaui.Services;

public class MediaControllerResolver : IMediaControllerResolver
{
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IMetaDataAnalyzer _metaDataAnalyzer;
    private readonly ICrypto _crypto;
    private readonly IDeclaratives _declaratives;

    public MediaControllerResolver(IStorageService storageService, IViewPlatforms viewPlatforms, IMetaDataAnalyzer metaDataAnalyzer, ICrypto crypto, IDeclaratives declaratives)
    {
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
        _metaDataAnalyzer = metaDataAnalyzer;
        _crypto = crypto;
        _declaratives = declaratives;
    }

    public IMediaPresentController ResolveMediaPresentController(MediaPresentVm vm, ISourceFile file)
    {
        return new MediaPresentController(vm, file, _declaratives, _viewPlatforms);
    }

    public IMediaPresentController ResolveMediaPresentLocal(MediaPresentVm vm, LocalDir? dir, LocalFile originPath)
    {
        return new MediaPresentLocal(vm, dir, originPath, _declaratives, _viewPlatforms);
    }

    public IMediaPresentController ResolveMediaPresentStorage(MediaPresentVm vm, StorageDir storageCell, StorageFile storageFile)
    {
        return new MediaPresentStorage(vm, storageCell, storageFile, _metaDataAnalyzer, _crypto, _storageService, _viewPlatforms, _declaratives);
    }
}