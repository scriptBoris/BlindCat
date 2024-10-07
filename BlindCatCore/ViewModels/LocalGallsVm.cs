using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class LocalGallsVm : BaseVm
{
    private readonly IStorageService _storageService;

    public class Key { }
    public LocalGallsVm(Key key, IStorageService storageService)
    {
        _storageService = storageService;
        Load();
    }

    public ObservableCollection<GalleryItem> Gals { get; set; } = [];

    public ICommand CommandOpenGal => new Cmd<GalleryItem>((galPath) =>
    {
        GoTo(new GalVm.Key { GallPath = galPath.Path });
    });

    private void Load()
    {
        string appdir = _storageService.AppDir;
        string galsPath = Path.Combine(appdir, "gals");
        if (Path.Exists(galsPath))
        {
            var gals = Directory.GetDirectories(galsPath);
            foreach (var file in gals)
            {
                Gals.Add(new GalleryItem
                {
                    Name = Path.GetFileName(file),
                    Path= file,
                });
            }
        }
    }
}