using BlindCatCore.Core;

namespace BlindCatCore.Models;

public class WebFile : BaseNotify, ISourceFile
{
    public int Id { get; set; }
    public required string FilePath { get; set; }
    public required string FileName { get; set; }
    public required string FileExtension { get; set; }
    public string? Description { get; set; }
    public string? FilePreview { get; set; }
    public StorageFile? TempStorageFile { get; set; }
    public bool IsSelected { get; set; }
    public required ISourceDir SourceDir { get; set; }
    public bool IsVideo => throw new NotImplementedException();
}