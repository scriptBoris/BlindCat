using BlindCatCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Models;

public class StorageAlbumItem : BaseNotify
{
    public required StorageFile StorageFile { get; set; }
    public bool IsCover { get; set; }
}