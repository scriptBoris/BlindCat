using BlindCatCore.Extensions;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using static BlindCatCore.Services.IViewModelResolver;
using static System.Net.Mime.MediaTypeNames;

namespace BlindCatCore.Core;

public class StorageDirController : IDisposable
{
    private readonly ObservableCollection<IStorageElement> _storageFiles = new();
    private readonly ObservableCollection<ISourceFile> _contentFiles = new();
    private readonly List<string> _indexedTags = new();

    public StorageDirController()
    {
        StorageFiles = new(_storageFiles);
        StorageContentFiles = new(_contentFiles);
    }

    public bool IsInitialized { get; private set; }
    public required StorageDir Storage { get; set; }

    /// <summary>
    /// Есть ли пароль который ввел пользователь для расшифровки данных. 
    /// Если есть, то это эти данные (пароль не зашифрован в ОЗУ)
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Инициализированные файлы которые содержат непосредственно контент
    /// (фото, видео и т.д.)
    /// </summary>
    public ReadOnlyObservableCollection<ISourceFile> StorageContentFiles { get; }

    /// <summary>
    /// Инициализированные элементы хранилища которые по умолчанию видит пользователь 
    /// при открытии хранилища
    /// (фото, видео, альбомы и т.д.)
    /// </summary>
    public ReadOnlyObservableCollection<IStorageElement> StorageFiles { get; }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public async Task InitFiles(IStorageElement[] humanVisibleItems, ISourceFile[] allContents)
    {
        _storageFiles.Clear();
        _contentFiles.Clear();

        foreach (var file in humanVisibleItems)
        {
            _storageFiles.Add(file);
            file.ListContext = _storageFiles;
        }

        foreach (var file in humanVisibleItems)
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

        foreach (var file in allContents)
        {
            _contentFiles.Add(file);
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
            char[] splits = new char[] { ' ', ',' };
            string[] words = searchText.Trim().Split(splits, StringSplitOptions.RemoveEmptyEntries);
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

    private bool HaveTags(string[] searchWords, IStorageElement file)
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

    private bool HaveAnyArtist(string[] words, IStorageElement file)
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

    public async Task<ObservableCollection<IStorageElement>?> Search(string text, CancellationToken cancel)
    {
        var res = await TaskExt.Run(() =>
        {
            char[] splits = new char[] { ' ', ',' };
            string[] searchWords = text.Split(splits, StringSplitOptions.RemoveEmptyEntries);
            var machedItems = _storageFiles
                .Where(x =>
                    string.Equals(x.Name, text, StringComparison.OrdinalIgnoreCase) ||
                    HaveAnyArtist(searchWords, x) ||
                    HaveTags(searchWords, x)
                );

            var res = new ObservableCollection<IStorageElement>(machedItems);
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

    public void OnFileDeleted(StorageFile file, bool invokeEvent)
    {
        _storageFiles.Remove(file);

        if (invokeEvent)
            Storage.OnElementRemoved(file);
    }

    public void MakeAlbum(StorageAlbum album, IEnumerable<ISourceFile> files)
    {
        foreach (var file in files)
        {
            _storageFiles.Remove((IStorageElement)file);
            Storage.OnElementRemoved(file);
        }

        _storageFiles.Insert(0, album);
        Storage.OnElementAdded(album);
    }

    public void MakeAlbumDeleted(StorageAlbum album, IEnumerable<ISourceFile> files, bool isPermoment)
    {
        if (isPermoment)
        {
            foreach (var file in files)
            {
                _storageFiles.Remove((IStorageElement)file);
                _contentFiles.Remove(file);
                Storage.OnElementRemoved(file);
            }

            _storageFiles.Remove(album);
            Storage.OnElementRemoved(album);
        }
        else
        {
            foreach (var file in files)
            {
                _storageFiles.Insert(0, (IStorageElement)file);
                Storage.OnElementAdded(file);
            }

            _storageFiles.Remove(album);
            Storage.OnElementRemoved(album);
        }
    }

    public async Task DeleteAlbum(StorageAlbum album, 
        IEnumerable<Guid> guidFiles, 
        IStorageService storageService,
        IDataBaseService dataBaseService,
        bool invokeEvents)
    {
        var files = _contentFiles
            .Where(x => guidFiles.Contains(((StorageFile)x).Guid))
            .Cast<StorageFile>();

        string pathDb = Storage.PathIndex;
        string password = Storage.Password;
        await dataBaseService.DeleteAlbum(pathDb, password, album);

        foreach (var file in files)
        {
            await storageService.DeleteFile(file);
        }

        if (invokeEvents)
        {
            Storage.OnElementRemoved(album);
        }
    }

    public void MakeAlbumMove(StorageAlbum album, StorageFile[] selected)
    {
        foreach (var item in selected)
        {
            _storageFiles.Remove(item);
            Storage.OnElementRemoved(item);
        }
    }

    public void MakeAlbumRemove(StorageAlbum album, IEnumerable<StorageFile> removed)
    {
        foreach (var item in removed)
        {
            _storageFiles.Insert(0, item);
            Storage.OnElementAdded(item);
        }
    }

    public StorageFile? GetNext(StorageFile by)
    {
        var slice = by.ListContext ?? StorageFiles;
        if (slice.Count <= 1)
            return null;

        int index = slice.IndexOf(by);
        int offset = 0;
        while (true)
        {
            int next = index + 1 + offset;
            if (next > slice.Count - 1)
                next = 0;

            var res = slice[next];
            if (res is StorageFile result)
            {
                return result;
            }
            else
            {
                offset++;
                if (next == offset)
                    return null;
            }
        }
    }

    public StorageFile? GetPrevious(StorageFile by)
    {
        var slice = by.ListContext ?? StorageFiles;
        if (slice.Count <= 1)
            return null;

        int index = slice.IndexOf(by);
        int offset = 0;
        while (true)
        {
            int prev = index - 1;
            if (prev < 0)
                prev = slice.Count - 1;

            var res = slice[prev];
            if (res is StorageFile result)
            {
                return result;
            }
            else
            {
                offset--;
                if (prev == offset) return null;
            }
        }
    }
}