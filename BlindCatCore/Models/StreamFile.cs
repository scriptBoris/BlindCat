namespace BlindCatCore.Models;

public class StreamFile : ISourceFile
{
    public int Id { get; set; }
    public required Stream Stream { get; set; }
    public string FilePath { get; }
    public string? Description { get; }
    public string FileName { get; }
    public string? FilePreview { get; }
    public string FileExtension { get; }
    public StorageFile? TempStorageFile { get; set; }
    public ISourceDir SourceDir { get; }
    public bool IsSelected { get; set; }
    public bool IsVideo { get; }
}