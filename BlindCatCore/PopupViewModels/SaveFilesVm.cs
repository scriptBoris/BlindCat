using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using System.Diagnostics;

namespace BlindCatCore.PopupViewModels;

public class SaveFilesVm : BaseVm<bool>
{
    private readonly ISourceFile[] _saveFiles;
    private readonly StorageDir _storageDir;
    private readonly IStorageService _storageService;
    private readonly IDeclaratives _declaratives;
    private readonly IProgressBroker<ISourceFile> _progressBroker;
    private readonly IFileUnlocker? _unlocker;

    public class Key : IKey<SaveFilesVm>
    {
        public required StorageDir StorageDir { get; set; }
        public required ISourceFile[] SaveFiles { get; set; }
        public IProgressBroker<ISourceFile>? ProgressBroker { get; set; }
        public IFileUnlocker? Unlocker { get; set; }
    }
    public SaveFilesVm(Key key, IStorageService storageService, IDeclaratives declaratives)
    {
        _saveFiles = key.SaveFiles;
        _storageDir = key.StorageDir;
        _storageService = storageService;
        _declaratives = declaratives;
        _progressBroker = key.ProgressBroker ?? new ProgressBroker<ISourceFile>();
        _unlocker = key.Unlocker;

        if (key.SaveFiles.Length == 1)
        {
            var f = key.SaveFiles[0];
            TagsController = new(key.SaveFiles[0].TempStorageFile!.Tags, TagFetchHandler);
            IsSingleFile = true;
            NameForSingleFile = Path.GetFileNameWithoutExtension(key.SaveFiles[0].FilePath);
            Artist = f.TempStorageFile!.Artist;
            Description = f.TempStorageFile!.Description;
        }
        else
        {
            TagsController = new([], TagFetchHandler);
            IsMultiFiles = true;
            LoadAlreadyTags();
        }

    }

    public bool IsSingleFile { get; }
    public bool IsMultiFiles { get; }
    public string StorageName => _storageDir.Name;
    public string? NameForSingleFile { get; set; }
    public string? Artist { get; set; }
    public string? Description { get; set; }

    public TagsController TagsController { get; }
    public TagCount[] AlreadyTags { get; private set; } = [];

    public IAsyncCommand CommandSave => new Cmd(async () =>
    {
        string? password = _storageDir.Password;
        if (password == null)
        {
            await ShowError("Storage is closed");
            return;
        }

        if (!string.IsNullOrWhiteSpace(TagsController.EntryText))
        {
            return;
        }

        using var loading = Loading("save", $"Saving {_saveFiles.Length} files to storage \"{StorageName}\"", null);
        string[] addTags = TagsController.SelectedTags.ToArray();

        // todo в будущем может понадобиться править здесь код
        if (IsSingleFile)
        {
            _saveFiles.First().TempStorageFile!.Name = NameForSingleFile;
        }
        else
        {
            foreach (var item in _saveFiles)
                item.TempStorageFile!.Name = Path.GetFileNameWithoutExtension(item.FilePath);
        }

        foreach (var item in _saveFiles)
        {
            item.TempStorageFile!.Artist = Artist;
            item.TempStorageFile!.Description = Description;
        }

        string exceptions = "";
        void Handler(object? item, ProgressBrokerProgress e)
        {
            if (!e.IsSuccess)
            {
                var f = (LocalFile)item;
                exceptions += $"{f.FileName}:\n{e.AppResponseError.MessageForLog}\n";
            }
        }

        _progressBroker.OnChanged += Handler;
        var res = await _declaratives.SaveLocalFiles(this, _saveFiles, addTags, _progressBroker, _unlocker);
        _progressBroker.OnChanged -= Handler;

        if (res.IsFault)
        {
            await HandleError(res);
        }
        else
        {
            int success = _saveFiles.Count(x => !x.TempStorageFile.IsTemp);
            int fails = _saveFiles.Count(x => x.TempStorageFile.IsTemp);

            if (fails == 0)
                await ShowMessage(
                    "Success",
                    $"All {success} of {_saveFiles.Length} was saved to storage \"{_storageDir.Name}\"",
                    "OK");
            else
                await ShowMessage(
                    "Success",
                    $"{success} of {_saveFiles.Length} was saved to storage \"{_storageDir.Name}\"\n" +
                    $"{fails} is can't (errors):\n\n{exceptions}",
                    "OK");
        }

        await SetResultAndPop(true);
    });

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

    public override Task<bool> TryClose()
    {
        if (IsLoading)
            return Task.FromResult(false);

        return base.TryClose();
    }

    private async void LoadAlreadyTags()
    {
        AlreadyTags = await DirPresentVm.FindAlreadyTags(_saveFiles);
    }

    private async Task<IEnumerable<string>?> TagFetchHandler(string tagName, CancellationToken cancel)
    {
        return await _storageDir.Controller!.SearchTag(_storageDir, tagName, cancel);
    }
}