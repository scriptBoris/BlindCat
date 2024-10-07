using BlindCatCore.Core;
using BlindCatCore.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.PopupViewModels;

[Obsolete("use EditTagsVm")]
public class RemoveTagsVm : BaseVm<bool>
{
    private ISourceFile[] _selectedFiles;

    public class Key : IKey<RemoveTagsVm>
    {
        public required ISourceFile[] SelectedFiles { get; set; }
        public required TagCount[] AlreadyTags { get; set; }
        public required StorageDir StorageDir { get; set; }
    }
    public RemoveTagsVm(Key key)
    {
        _selectedFiles = key.SelectedFiles;
        AlreadyTags = new (key.AlreadyTags);
        CommandDeleteTag = new Cmd<TagCount>(ActionDeleteTag);
        CommandCancelDeleteTag = new Cmd<TagCount>(ActionCancelDeleteTag);
    }

    public ObservableCollection<TagCount> AlreadyTags { get; }
    public ObservableCollection<TagCount> WillDeletedTags { get; } = new();

    public ICommand CommandDeleteTag { get; private set; }
    public void ActionDeleteTag(TagCount tag)
    {
        AlreadyTags.Remove(tag);
        WillDeletedTags.Add(tag);
    }

    public ICommand CommandCancelDeleteTag { get; private set; }
    public void ActionCancelDeleteTag(TagCount tag)
    {
        AlreadyTags.Add(tag);
        WillDeletedTags.Remove(tag);
    }

    public IAsyncCommand CommandSave => new Cmd(async () =>
    {
        if (WillDeletedTags.Count == 0)
            return;

        foreach (var file in _selectedFiles)
        {
            var newTags = new List<string>(file.TempStorageFile!.Tags);

            foreach (var tag in WillDeletedTags)
            {
                bool alreadyExist = file.TempStorageFile.Tags.Any(x => string.Equals(tag.TagName, x, StringComparison.OrdinalIgnoreCase));
                if (alreadyExist)
                {
                    newTags.Remove(tag.TagName);
                }
            }

            // update tags
            file.TempStorageFile.Tags = newTags.ToArray();
        }

        await Close();
    });
}