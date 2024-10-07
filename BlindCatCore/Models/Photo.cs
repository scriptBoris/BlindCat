using BlindCatCore.Core;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Models;

public class Photo : BaseNotify
{
    public int Number { get; set; }
    public required string Preview {  get; set; }
    public required string SourcePrepareURL { get; set; }
    public string? Source { get; set; }

    public bool IsError { get; set; }
    public bool IsDownloading { get; set; }
    public string? ErrorText { get; set; }
    public string? DownloadSourceProgress_Text { get; set; }

    private string? _message;
    [DependsOn(nameof(IsError), nameof(IsDownloading))]
    public string? Message
    {
        get
        {
            if (IsError)
                return ErrorText;

            if (IsDownloading)
                return DownloadSourceProgress_Text;

            return _message ?? "OK";
        }
        set => _message = value;
    }
}
