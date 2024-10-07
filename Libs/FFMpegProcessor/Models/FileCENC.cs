using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegProcessor.Models;

public class FileCENC
{
    public required string FilePath {  get; init; }
    public required string Key {  get; init; }
    public required string Kid { get; init; }
}