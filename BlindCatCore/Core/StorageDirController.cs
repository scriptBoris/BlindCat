using BlindCatCore.Extensions;
using BlindCatCore.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json.Serialization;
using static BlindCatCore.Services.IViewModelResolver;
using static System.Net.Mime.MediaTypeNames;

namespace BlindCatCore.Core;

public class StorageDirController : IDisposable
{
    private readonly ObservableCollection<StorageFile> _storageFiles = new();
    private List<string> _indexedTags = new();

    public StorageDirController()
    {
        StorageFiles = new(_storageFiles);
    }

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Есть ли пароль который ввел пользователь для расшифровки данных. 
    /// Если есть, то это эти данные (пароль не зашифрован в ОЗУ)
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Инициализированные файлы хранилища
    /// </summary>
    public ReadOnlyObservableCollection<StorageFile> StorageFiles { get; }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public async Task InitFiles(StorageFile[] result)
    {
        _storageFiles.Clear();
        foreach (var file in result)
        {
            _storageFiles.Add(file);
            file.ListContext = _storageFiles;
        }

        foreach (var file in result)
        {
            if (file.Tags.Length == 0)
                continue;

            await Task.Run(() =>
            {
                foreach (string tag in file.Tags)
                {
                    if (!_indexedTags.Contains(tag))
                        _indexedTags.Add(tag);
                }
            });
        }
        IsInitialized = true;
    }

    public Task<IEnumerable<string>?> SearchTag(StorageDir storageDir, string searchText, CancellationToken token)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("SecureStorage is not initialized!");

        if (_indexedTags.Count == 0 || string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<string>?>([]);

        var res = TaskExt.Run<IEnumerable<string>>(() =>
        {
            string[] words = searchText.Trim().Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            var priority0 = new List<string>();
            var priority1 = new List<string>();
            var priorityRegular = new List<string>();
            const int maxSuggestions = 7;

            foreach (var tag in _indexedTags)
            {
                if (tag.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    priority0.Add(tag);

                    if (priority0.Count >= maxSuggestions)
                        goto endloops;
                    else
                        continue;
                }

                foreach (var word in words)
                {
                    if (word.Length <= 2)
                    {
                        if (tag.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                        {
                            priorityRegular.Add(tag);
                        }
                        else if (string.Equals(tag, word, StringComparison.OrdinalIgnoreCase))
                        {
                            priority1.Add(tag);
                        }
                    }
                    else if (tag.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        priorityRegular.Add(tag);
                    }

                    //if (priorityRegular.Count >= maxSuggestions)
                    //    goto endloops;

                    //return results
                    //    .Distinct()
                    //    .OrderBy(x => x.Length);
                }
            }

        endloops:
            var outResupt = priorityRegular
                .Distinct()
                .OrderBy(x => x.Length)
                .ToList();

            if (priority1.Count > 0)
            {
                var tmp = priority1
                    .Distinct()
                    .OrderBy(x => x.Length);

                outResupt.InsertRange(0, tmp);
            }

            if (priority0.Count > 0)
            {
                var tmp = priority0
                    .Distinct()
                    .OrderBy(x => x.Length);

                outResupt.InsertRange(0, tmp);
            }

            return outResupt.Take(maxSuggestions);
        }, token);

        return res;
    }

    private bool HaveAnyTags(string[] words, StorageFile file)
    {
        if (file.Tags.Length == 0)
            return false;

        return file.Tags.Any(x =>
        {
            foreach (var tag in words)
            {
                if (x.Contains(tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        });
    }

    private bool HaveTags(string[] searchWords, StorageFile file)
    {
        if (file.Tags.Length == 0)
            return false;
        int match = 0;
        foreach (var searchWord in searchWords)
        {
            foreach (var tag in file.Tags)
            {
                if (tag.Contains(searchWord, StringComparison.OrdinalIgnoreCase))
                {
                    match++;
                    break;
                }
            }
        }

        if (match == searchWords.Length)
        {
            return true;
        }
        return false;
    }

    private bool HaveAnyArtist(string[] words, StorageFile file)
    {
        if (file.Artist == null)
            return false;

        foreach (var word in words)
        {
            if (file.Artist.Contains((string)word, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public async Task<ObservableCollection<StorageFile>?> Search(string text, CancellationToken cancel)
    {
        var res = await TaskExt.Run(() =>
        {
            string[] searchWords = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            var machedItems = _storageFiles
                .Where(x =>
                    string.Equals(x.Name, text, StringComparison.OrdinalIgnoreCase) ||
                    HaveAnyArtist(searchWords, x) ||
                    HaveTags(searchWords, x)
                );

            var res = new ObservableCollection<StorageFile>(machedItems);
            //foreach (var item in res)
            //    item.ListContext = res;
            return res;
        }, cancel);

        return res;
    }

    public async Task AddTags(string[] tags)
    {
        var adds = new List<string>();
        foreach (string newTag in tags)
        {
            string t = newTag.Trim();
            bool isMatch = await Task.Run(() =>
            {
                return _indexedTags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase));
            });
            if (!isMatch)
            {
                _indexedTags.Add(t);
            }
        }
    }

    public void AddFile(StorageFile newStorage)
    {
        _storageFiles.Add(newStorage);
    }

    public void DeleteFile(StorageFile file)
    {
        _storageFiles.Remove(file);
    }

    public StorageFile? GetNext(StorageFile by)
    {
        var slice = by.ListContext ?? StorageFiles;
        if (slice.Count <= 1)
            return null;

        int index = slice.IndexOf(by);
        int next = index + 1;
        if (next > slice.Count - 1)
            next = 0;

        return slice[next];
    }

    public StorageFile? GetPrevious(StorageFile by)
    {
        var slice = by.ListContext ?? StorageFiles;
        if (slice.Count <= 1)
            return null;

        int index = slice.IndexOf(by);
        int prev = index - 1;
        if (prev < 0)
            prev = slice.Count - 1;

        return slice[prev];
    }

    [Obsolete("Нужно ли?")]
    public class DependedObsList : ObservableCollection<StorageFile>, IDisposable
    {
        private ObservableCollection<StorageFile> _dpendency;

        public DependedObsList(IList<StorageFile> by, ObservableCollection<StorageFile> dpendency)
        {
            _dpendency = dpendency;
            _dpendency.CollectionChanged += OnCollectionChanged;

            foreach (var item in by)
            {
                var clone = new StorageFile
                {
                    FilePath = item.FilePath,
                    Storage = item.Storage,
                    Artist = item.Artist,
                    CachedMediaFormat = item.CachedMediaFormat,
                    DateInitIndex = item.DateInitIndex,
                    DateLastIndex = item.DateLastIndex,
                    Description = item.Description,
                    FilePreview = item.FilePreview,
                    Guid = item.Guid,
                    Id = item.Id,
                    IsIndexed = item.IsIndexed,
                    IsNoDBRow = item.IsNoDBRow,
                    IsNoFile = item.IsNoFile,
                    IsSelected = item.IsSelected,
                    IsTemp = item.IsTemp,
                    Name = item.Name,
                    Tags = item.Tags,
                    ListContext = by,
                };
                this.Add(clone);
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    break;
                case NotifyCollectionChangedAction.Remove:
                    var rm = (StorageFile)e.OldItems![0]!;
                    this.Remove(rm);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    break;
                default:
                    break;
            }
        }

        public void Dispose()
        {
            _dpendency.CollectionChanged -= OnCollectionChanged;
            _dpendency = null!;
            GC.SuppressFinalize(this);
        }
    }
}