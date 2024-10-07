namespace BlindCatCore.Core;

public interface IFileUnlocker
{
    Task<AppResponse> UnlockFile(string filePath);
}