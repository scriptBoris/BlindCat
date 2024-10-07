using BlindCatCore.Core;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace BlindCatCore.Models;

public class LocalDir : BaseNotify, ISourceDir
{
    private ObservableCollection<LocalFile>? _files;

    public event EventHandler<ISourceFile>? FileDeleting;
    public event EventHandler<ISourceFile>? FileDeleted;

    public required string DirPath { get; set; }

    public ObservableCollection<LocalFile>? FilesSource 
    {
        set
        {
            if (value == null)
            {
                _files = null;
                Files = null;
            }
            else
            {
                _files = new(value);
                Files = new ReadOnlyObservableCollection<LocalFile>(_files);
            }
        }
    }

    public ReadOnlyObservableCollection<LocalFile>? Files { get; private set; }

    public LocalFile? GetPrevious(LocalFile currentFile)
    {
        int count = Files?.Count ?? 0;
        if (count <= 1)
            return null;

        int currentId = Files!.IndexOf(currentFile);
        if (currentId == -1) 
            return null;

        int prevId = currentId - 1;
        if (prevId == -1) 
            return null;

        return Files[prevId];
    }

    public LocalFile? GetNext(LocalFile currentFile)
    {
        int count = Files?.Count ?? 0;
        if (count <= 1)
            return null;

        int currentId = Files!.IndexOf(currentFile);
        if (currentId < 0)
            return null;

        int nextId = currentId + 1;
        if (nextId > count -1 )
            return null;

        return Files[nextId];
    }

    public ISourceFile? GetNext(ISourceFile by)
    {
        return GetNext((LocalFile)by);
    }

    public ISourceFile? GetPrevious(ISourceFile by)
    {
        return GetPrevious((LocalFile)by);
    }

    public IList<ISourceFile> GetAllFiles()
    {
        return (IList<ISourceFile>)Files!;
    }

    public void Remove(ISourceFile file)
    {
        bool exists = _files?.Contains((LocalFile)file) ?? false;
        if (!exists)
            return;

        FileDeleting?.Invoke(this, file);
        _files!.Remove((LocalFile)file);
        FileDeleted?.Invoke(this, file);
    }
}