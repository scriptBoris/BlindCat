using BlindCatCore.Core;
using BlindCatCore.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.ViewModels.Panels;

public class FileInfoPanelVm : BaseVm
{
    private ObservableCollection<FileMetaData>? _metaData;
    public class Key : IKey<FileInfoPanelVm>
    {
        public required ISourceFile File { get; set; }
    }

    public FileInfoPanelVm(Key key)
    {
        File = key.File;
    }

    #region props
    public ISourceFile File { get; set; }
    public IReadOnlyCollection<FileMetaData>? Meta { get; private set; }
    #endregion props

    public void ClearAndSetMeta(FileMetaData[]? meta)
    {
        if (meta == null)
        {
            _metaData = null;
            Meta = null;
        }
        else
        {
            _metaData = new ObservableCollection<FileMetaData>(meta);
            Meta = _metaData;
        }
    }

    public void UseMeta(FileMetaData[]? meta)
    {
        if (meta == null)
            return;

        bool notify = _metaData == null;
        _metaData ??= new ObservableCollection<FileMetaData>();

        foreach (var newGroup in meta)
        {
            var mGroup = _metaData.FirstOrDefault(x => x.GroupName == newGroup.GroupName);
            if (mGroup == null)
            {
                mGroup = newGroup;

                if (newGroup.GroupName == "Meta")
                    _metaData.Insert(0, newGroup);
                else
                    _metaData.Add(newGroup);
            }

            foreach (var item in newGroup.MetaItems)
            {
                var exist = mGroup.MetaItems.FirstOrDefault(x => x.Meta == item.Meta);
                if (exist != null)
                {
                    exist.Value = item.Value;
                }
                else 
                {
                    if (item.Meta == "File size")
                        mGroup.MetaItems.Insert(0, item);
                    else
                        mGroup.MetaItems.Add(item);
                }
            }
        }

        if (notify)
        {
            Meta = _metaData;
        }
    }
}