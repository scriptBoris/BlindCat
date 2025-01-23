using BlindCatCore.Core;
using BlindCatCore.Models;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BlindCatCore.PopupViewModels;

public class SelectStorageAlbumVm : BaseVm<StorageAlbum>
{
    private readonly StorageDir _storage;
    private StorageAlbum? _oldSelected;
    public class Key : IKey<SelectStorageAlbumVm>
    {
        public Key() { }
        public required StorageDir StorageDir { get; set; }
    }
    public SelectStorageAlbumVm(Key key)
    {
        _storage = key.StorageDir;

        var tmp = key.StorageDir
            .Controller
            .StorageFiles
            .Where(x => x is StorageAlbum)
            .Cast<StorageAlbum>();

        var albums = new List<StorageAlbum>();
        foreach (var x in tmp)
        {
            var clone = new StorageAlbum
            {
                Guid = x.Guid,
                SourceDir = x.SourceDir,
                DateCreated = x.DateCreated,
                DateModified = x.DateModified,
                FilePreview = x.FilePreview,
                CoverGuid = x.CoverGuid,
                Name = x.Name,
                Description = x.Description,
                Artist = x.Artist,
            };
            albums.Add(clone);
        }
        Albums = albums;

        CommandTapItem = new Cmd<StorageAlbum>(ActionSelectedChanged);
        CommandSelectedChanged = new Cmd<StorageAlbum>(ActionSelectedChanged);
    }

    public List<StorageAlbum> Albums { get; private set; }

    public ICommand CommandTapItem { get; init; }
    public ICommand CommandSelectedChanged { get; init; }
    private void ActionSelectedChanged(StorageAlbum album)
    {
        if (_oldSelected != null)
            _oldSelected.IsSelected = false;

        album.IsSelected = true;
        _oldSelected = album;
    }

    public ICommand CommandCancel => new Cmd(() =>
    {
        Close();
    });

    public ICommand CommandAccept => new Cmd(() =>
    {
        var selected = _oldSelected;
        if (selected != null)
        {
            var existAlbum = _storage
                .Controller
                .StorageFiles
                .Where(x => x is StorageAlbum)
                .Cast<StorageAlbum>()
                .FirstOrDefault(x => x.Guid == selected.Guid);

            if (existAlbum != null)
                SetResultAndPop(existAlbum);
        }
    });
}