using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.ViewModels;

public class GalVm : BaseVm
{
    private readonly Key _key;
    private readonly IStorageService _storageService;
    private readonly string _previewDir;

    public class Key
    {
        public required string GallPath { get; init; }
    }
    public GalVm(Key key, IStorageService storageService)
    {
        _key = key;
        _storageService = storageService;
        _previewDir = Path.Combine(key.GallPath, "previews");

        CommandTapItem = new Cmd<GalPhoto>(ActionTapItem);
        Load();
    }

    public ObservableCollection<GalPhoto> Photos { get; set; } = [];

    #region commands
    public ICommand CommandExport => new Cmd(async () =>
    {
        using var busy = Loading();
        string[] files = Photos.Select(x => x.PhotoPath).ToArray();
        await _storageService.ExportFiles(files);
    });

    public ICommand CommandTapItem { get; set; }
    private Task ActionTapItem(GalPhoto photo)
    {
        throw new NotImplementedException();
        //return GoTo(new MediaPresentVm.Key 
        //{
        //    OriginPath = photo.PhotoPath
        //});
    }
    #endregion commands

    private async void Load()
    {
        using var busy = Loading();
        var files = Directory.GetFiles(_key.GallPath);
        foreach (var photoPath in files)
        {
            string prev = Path.Combine(_previewDir, Path.GetFileName(photoPath));
            GalPhoto photo;
            if (File.Exists(prev))
            {
                photo = new GalPhoto
                {
                    PhotoPath = photoPath,
                    PreviewPath = prev,
                };
            }
            else
            {
                photo = new GalPhoto
                {
                    PhotoPath = photoPath,
                    PreviewPath = null,
                };
            }
            Photos.Add(photo);
        }
        await Task.Delay(200);
    }
}
