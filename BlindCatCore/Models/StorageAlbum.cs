using BlindCatCore.Enums;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Models;

public class StorageAlbum : IStorageElement, ISourceDir
{
    private readonly object _locker = new();

    public Guid Guid { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public string? Artist { get; set; }
    public string? FilePreview { get; set; }
    public EncryptionMethods EncryptionMethod => EncryptionMethods.dotnet;
    public MediaFormats CachedMediaFormat => MediaFormats.Unknown;
    public bool IsSelected { get; set; }
    public required ISourceDir SourceDir { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public IList<IStorageElement>? ListContext { get; set; }
    public Guid? CoverGuid { get; set; }

    [DependsOn(nameof(Name))]
    public string FileName => $"{Name}";

    public bool IsVideo => false;
    public bool IsAlbum => true;
    public bool IsIndexed { get; set; }
    public bool IsErrorNoDBRow { get; set; }
    public bool IsErrorNoFile { get; set; }
    public List<Guid> Contents { get; } = new();
    public IList<ISourceFile>? InitializedContents { get; set; }
    public int ChildrenCount => Contents.Count;

    #region source dir
    public event EventHandler<ISourceFile>? FileDeleting;
    public event EventHandler<ISourceFile>? FileDeleted;
    public event EventHandler<object>? ElementAdded;

    public IList<ISourceFile> GetAllFiles()
    {
        return InitializedContents ?? throw new InvalidOperationException();
    }

    public ISourceFile? GetNext(ISourceFile by)
    {
        if (InitializedContents == null)
            return null;

        var slice = InitializedContents;
        if (slice.Count <= 1)
            return null;

        int index = slice.IndexOf(by);
        int next = index + 1;
        if (next > slice.Count - 1)
            next = 0;

        return slice[next];
    }

    public ISourceFile? GetPrevious(ISourceFile by)
    {
        if (InitializedContents == null)
            return null;

        var slice = InitializedContents;
        if (slice.Count <= 1)
            return null;

        int index = slice.IndexOf(by);
        int next = index - 1;
        if (next < 0)
            next = slice.Count - 1;

        return slice[next];
    }

    public void Remove(ISourceFile file)
    {
        throw new NotImplementedException();
    }

    #endregion source dir

    public void SafeAdd(StorageFile matchFile)
    {
        lock (_locker)
        {
            if (FilePreview == null)
            {
                FilePreview = matchFile.FilePreview;
            }
            Contents.Add(matchFile.Guid);
        }
    }
}