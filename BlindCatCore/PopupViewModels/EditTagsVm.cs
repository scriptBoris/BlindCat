using BlindCatCore.Core;
using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace BlindCatCore.PopupViewModels;

public class EditTagsVm : BaseVm<bool>
{
    private readonly StorageDir _storage;
    private readonly ISourceFile[] _selectedFiles;
    private readonly IStorageService _storageService;

    public class Key : IKey<EditTagsVm>
    {
        public required ISourceFile[] SelectedFiles { get; set; }
        public required TagCount[] AlreadyTags { get; set; }
        public required StorageDir StorageDir { get; set; }
    }
    public EditTagsVm(Key key, IStorageService storageService)
    {
        _storage = key.StorageDir;
        _selectedFiles = key.SelectedFiles;
        _storageService = storageService;
        TagsController = new TagsController([], TagFetchHandler);
        AlreadyTags = key.AlreadyTags.ToObs();
        CommandRemoveTag = new Cmd<object>(ActionRemoveTag);
    }

    #region props
    public TagsController TagsController { get; set; }
    public ObservableCollection<TagCount> AlreadyTags { get; }
    public ObservableCollection<TagCount> WillRemovedTags { get; } = new();
    #endregion props

    #region commands
    public IAsyncCommand CommandSave => new Cmd(async () =>
    {
        if (IsLoading)
            return;

        using var loading = Loading();
        if (TagsController.SelectedTags.Count == 0 && WillRemovedTags.Count == 0)
        {
            await Close();
            return;
        }

        string[] alreadyTags = AlreadyTags.Select(x => x.TagName).ToArray();
        string[] tryAddTags = TagsController.SelectedTags.ToArray();
        string[] tryRemoveTags = WillRemovedTags.Select(x => x.TagName).ToArray();

        var resps = new List<AppResponse>();
        string password = _storage.Password ?? throw new UnauthorizedAccessException();
        foreach (var file in _selectedFiles)
        {
            file.TempStorageFile!.Tags = TagsController.Merge(file.TempStorageFile!.Tags, tryAddTags, tryRemoveTags);

            // если файл уже сохранен, то обновляем тэги
            if (file.TempStorageFile.IsIndexed)
            {
                var res = await _storageService.UpdateStorageFile(_storage, file.TempStorageFile, password);
                resps.Add(res);
            }
        }

        if (resps.Any(x => x.IsFault))
        {
            await ShowError("Some files could not be updated");
        }

        await SetResultAndPop(true);
    });

    public ICommand CommandRemoveTag {  get; init; }
    private void ActionRemoveTag(object tag)
    {
        // Если тэг убрали из "новых тэгов"
        if (tag is string newTag)
        {
            TagsController.SelectedTags.Remove(newTag);
        }
        else if (tag is TagCount tagCount)
        {
            // Если тэг убрали из уже существующих тэгов
            if (AlreadyTags.Remove(tagCount))
            {
                WillRemovedTags.Add(tagCount);
            }
            // Если тэг убрали из "тэги будут удалены"
            else if (WillRemovedTags.Remove(tagCount))
            {
                AlreadyTags.Add(tagCount);
            }
        }
    }
    #endregion commands

    public override void OnKeyComboListener(KeyPressedArgs args)
    {
        base.OnKeyComboListener(args);
        switch (args.Key)
        {
            case "Ctrl+Enter":
                CommandSave.Execute(null);
                break;
            default:
                break;
        }
    }

    public async Task<IEnumerable<string>?> TagFetchHandler(string tagName, CancellationToken cancel)
    {
        if (_storage.Controller == null)
            throw new InvalidOperationException("Storage is closed");

        return await _storage.Controller.SearchTag(_storage, tagName, cancel);
    }
}