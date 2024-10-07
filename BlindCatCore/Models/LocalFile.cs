using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.ViewModels;

namespace BlindCatCore.Models;

public class LocalFile : BaseNotify, ISourceFile
{
    public int Id { get; set; }
    public required string FilePath { get; set; }
    public string FileName => Path.GetFileName(FilePath);
    public string? Description { get; set; }
    public string FileExtension => Path.GetExtension(FilePath).ToLower();
    public StorageFile? TempStorageFile { get; set; }
    public ISourceDir SourceDir => Dir;
    public bool IsSelected { get; set; }
    public bool IsVideo => MediaPresentVm.ResolveFormat(FilePath).IsVideo();
    public string? FilePreview => null;

    /// <summary>
    /// Директория в которой находится данный файл 
    /// (если была индексация)
    /// </summary>
    public LocalDir? Dir { get; set; }
}