using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BlindCatCore.PopupViewModels;

public class EditMetaVm : BaseVm
{
    private readonly StorageFile _file;
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;
    private readonly StorageDir? _storageDir;

    public class Key
    {
        public required StorageFile File { get; set; }
    }
    public EditMetaVm(Key key, IStorageService storageService, IViewPlatforms viewPlatforms)
    {
        _file = key.File;
        _storageDir = key.File.SourceDir as StorageDir;
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
        Name = key.File.Name ?? "";
        Artist = key.File.Artist ?? "";
        Description = key.File.Description ?? "";
        TagsController = new TagsController(key.File.Tags, TagFetchHandler);
    }

    #region props
    public string Name { get; set; }
    public string Artist { get; set; }
    public string Description { get; set; }
    public TagsController TagsController { get; }
    #endregion props

    public ICommand CommandSave => new Cmd(async () =>
    {
        using var busy = Loading();
        string password = _file.Storage.Password!;

        if (!await _storageService.CheckPasswordCorrect(_storageDir, password))
        {
            await _viewPlatforms.ShowDialog("Error", "Password is incorrect", "OK", View);
            return;
        }

        _file.Name = Name;
        _file.Artist = Artist;
        _file.Description = Description;
        _file.Tags = TagsController.SelectedTags.ToArray();

        AppResponse res;
        if (_file.IsTemp)
        {
            res = await _storageService.SaveStorageFile(_storageDir, _file, password, null);
        }
        else
        {
            res = await _storageService.UpdateStorageFile(_storageDir, _file, password);
        }

        if (res.IsFault)
        {
            await HandleError(res);
            return;
        }

        await Close();
    });

    private Task<IEnumerable<string>?> TagFetchHandler(string tagName, CancellationToken cancel)
    {
        return _file.Storage.Controller!.SearchTag(_file.Storage, tagName, cancel);
    }
}