using BlindCatCore.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Models;

public class FileMetaData
{
    public required string GroupName { get; set; }
    public required ObservableCollection<FileMetaItem> MetaItems { get; set; }
}

public class FileMetaItem : BaseNotify
{
    public required string Meta { get; set; }
    public required string Value { get; set; }
}

public static class FileMetaStatic
{


    public static void TryAdd(this IList<FileMetaItem> self, string meta, string? value)
    {
        if (value == null)
            return;

        self.Add(new FileMetaItem
        {
            Meta = meta,
            Value = value,
        });
    }
}