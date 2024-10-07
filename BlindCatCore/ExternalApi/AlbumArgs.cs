using BlindCatCore.Models;

namespace BlindCatCore.ExternalApi;

public class AlbumArgs
{
    public required IList<ISourceFile> Items { get; set; }
    public required string Subtitle { get; set; }
    public required ISourceDir Dir { get; set; }
}