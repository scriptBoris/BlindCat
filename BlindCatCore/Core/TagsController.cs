using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BlindCatCore.Core;

public delegate Task<IEnumerable<string>?> TagFetchHandler(string tagName, CancellationToken cancel);

public class TagsController : BaseNotify
{
    private readonly List<string> newTags = new();
    private readonly TagFetchHandler _search;
    private string? _selectedItem = null;
    private CancellationTokenSource _cancellationTokenSource = new();

    public TagsController(string[] selectedTags, TagFetchHandler search)
    {
        _search = search;
        CommandTextChanged = new Cmd<string>(ActionTextChanged);
        CommandPressEnter = new Cmd<string>(ActionPressEnter);
        CommandTagDelete = new Cmd<string>(ActionTagDelete);
        SelectedTags = new(selectedTags);
        HasSelectedTags = selectedTags.Length > 0;
    }

    public ObservableCollection<string> SelectedTags { get; private set; } = new();
    public ObservableCollection<string> FilteredTags { get; private set; } = new();
    public bool HasSelectedTags { get; private set; }
    public string? EntryText { get; set; }
    public string? SelectedItem { get; set; }
    //{
    //    get => _selectedItem;
    //    set
    //    {
    //        if (value == null)
    //            return;

    //        EntryText = null;
    //        _selectedItem = null;
    //        if (!SelectedTags.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
    //        {
    //            _cancellationTokenSource.Cancel();
    //            _cancellationTokenSource = new();
    //            SelectedTags.Add(value);
    //            newTags.Add(value);
    //        }
    //    }
    //}

    public ICommand CommandPressEnter { get; init; }
    private void ActionPressEnter(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();

        if (!SelectedTags.Any(x => string.Equals(x, input, StringComparison.OrdinalIgnoreCase)))
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new();
            //EntryText = null;
            SelectedTags.Add(input);
            newTags.Add(input);
            FilteredTags = new();
            HasSelectedTags = true;
        }
    }

    public ICommand CommandTagDelete { get; init; }
    private void ActionTagDelete(string tag)
    {
        SelectedTags.Remove(tag);
        HasSelectedTags = SelectedTags.Count > 0;
    }

    public ICommand CommandTextChanged { get; init; }
    private async void ActionTextChanged(string text)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();
        //EntryText = text;
        //SelectedItem = null;
        var m = await _search(text, _cancellationTokenSource.Token);
        if (m != null)
        {
            FilteredTags = new(m);
        }
    }

    public static string[] Merge(string[] alreadyTags, string[] tryAddTags, string[] tryRemoveTags)
    {
        var finish = new List<string>();

        foreach (var alreadyTag in alreadyTags)
        {
            bool isRemoved = tryRemoveTags.Any(x => string.Equals(alreadyTag, x, StringComparison.OrdinalIgnoreCase));
            if (isRemoved)
            {
                continue;
            }
            else
            {
                finish.Add(alreadyTag);
            }
        }

        foreach (string tag in tryAddTags)
        {
            bool alreadyExist = alreadyTags.Any(x => string.Equals(tag, x, StringComparison.OrdinalIgnoreCase));
            if (!alreadyExist)
            {
                finish.Add(tag);
            }
        }

        return finish.ToArray();
    }
}