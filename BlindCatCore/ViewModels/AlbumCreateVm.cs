using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using ValidatorSam;

namespace BlindCatCore.ViewModels;

public class AlbumCreateVm : BaseVm
{
    private readonly IDataBaseService _dataBaseService;
    private readonly StorageDir _storageDir;

    public class Key
    {
        public Key() { }
        public required StorageFile[] Files { get; set; }
        public required StorageDir StorageDir { get; set; }
    }
    public AlbumCreateVm(Key key, IDataBaseService dataBaseService)
    {
        _dataBaseService = dataBaseService;
        _storageDir = key.StorageDir;
        Files = new(key.Files);
    }

    public Validator<string> Name => Validator<string>.Build()
        .UsingRequired();

    public ObservableCollection<StorageFile> Files { get; private set; }

    public ICommand CommandAccept => new Cmd(async () =>
    {
        string path = _storageDir.PathIndex;
        string? pass = _storageDir.Password;
        if (pass == null)
            return;

        var album = new StorageAlbum
        {
            Name = Name.Value,
            Description = null,
            Artist = null,
            Tags = [],
            SourceDir = _storageDir,
            DateCreated = DateTime.Now,
            DateModified = DateTime.Now,
        };

        foreach (var file in Files)
        {
            album.Contents.Add(file.Guid);
        }

        using var loading = Loading();
        var res = await _dataBaseService.CreateAlbum(path, pass, album);
        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        _storageDir.Controller?.MakeAlbum(album, Files);
        await Close();
    });
}