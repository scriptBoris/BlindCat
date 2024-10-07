using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using System.Windows.Input;

namespace BlindCatCore.Core;

/// <summary>
/// Контроллер для Storage file meta panel
/// </summary>
[Obsolete("Не нужно, использовать лучше ViewModel")]
public class StorageFileController : BaseNotify
{
    private readonly BaseVm _vm;
    private readonly StorageDir _storageDir;
    private readonly StorageFile _storageFile;
    private readonly IStorageService _storageService;
    private readonly IViewPlatforms _viewPlatforms;

    //public event EventHandler<MediaPresentControllerProps>? PresentPropChanged;

    public StorageFileController(BaseVm vm, StorageFile file, IStorageService storageService, IViewPlatforms viewPlatforms)
    {
        _vm = vm;
        _storageFile = file;
        _storageDir = file.Storage;
        _storageService = storageService;
        _viewPlatforms = viewPlatforms;
        CommandSave = new Cmd(ActionSave);
        FileName = file.Name;
        Author = file.Artist;
        Description = file.Description;
        TagsController = new TagsController(file.Tags, SearchTag);
        StorageName = _storageDir.Name;

        if (file.IsIndexed)
        {
            ButtonSaveText = "Save now";
        }
        else
        {
            ButtonSaveText = "Save and Indexed";
        }
    }

    #region props
    public string StorageName {  get; }
    public string? FileName { get; set; }
    public string? Author { get; set; }
    public TagsController TagsController { get; private set; }
    public string? Description { get; set; }
    public string ButtonSaveText { get; private set; }
    #endregion props

    #region commands
    public IAsyncCommand CommandSave { get; private set; }
    private async Task ActionSave()
    {
        string password = _storageFile.Storage.Password!;

        if (!await _storageService.CheckPasswordCorrect(_storageDir, password))
        {
            await _viewPlatforms.ShowDialog("Error", "Password is incorrect", "OK", null);
            return;
        }

        _storageFile.Name = FileName;
        _storageFile.Artist = Author;
        _storageFile.Description = Description;
        _storageFile.Tags = TagsController.SelectedTags.ToArray();

        if (_vm is MediaPresentVm vm)
        {
            vm.View.Stop();
        }

        AppResponse res;
        if (_storageFile.IsTemp)
        {
            res = await _storageService.SaveStorageFile(_storageDir, _storageFile, password, null);
        }
        else
        {
            res = await _storageService.UpdateStorageFile(_storageFile.Storage, _storageFile, password);
        }

        if (res.IsFault)
        {
            await _vm.HandleError(res);
            return;
        }

        //PresentPropChanged?.Invoke(this, MediaPresentControllerProps.FileName);
        //PresentPropChanged?.Invoke(this, MediaPresentControllerProps.IsIndexed);
    }
    #endregion commands

    private async Task<IEnumerable<string>?> SearchTag(string tag, CancellationToken cancellationToken)
    {
        if (_storageFile.Storage.IsClose)
            return null;

        return await _storageFile.Storage.Controller.SearchTag(_storageFile.Storage, tag, cancellationToken);
    }
}