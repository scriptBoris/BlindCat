namespace BlindCatCore.Models;

public interface ISourceDir
{
    event EventHandler<ISourceFile>? FileDeleting;
    event EventHandler<ISourceFile>? FileDeleted;
    event EventHandler<object>? ElementAdded;

    IList<ISourceFile> GetAllFiles();
    ISourceFile? GetNext(ISourceFile by);
    ISourceFile? GetPrevious(ISourceFile by);
    void Remove(ISourceFile file);
}