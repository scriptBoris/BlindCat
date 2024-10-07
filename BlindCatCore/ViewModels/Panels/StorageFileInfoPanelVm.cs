using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BlindCatCore.ViewModels.Panels;

public class StorageFileInfoPanelVm : BaseVm
{
    public class Key : IKey<StorageFileInfoPanelVm>
    {
        public required StorageFile File { get; set; }
        public required ICommand CommandChangeEncryption { get; set; }

    }
    public StorageFileInfoPanelVm(Key key)
    {
        File = key.File;
        CommandChangeEncryption = key.CommandChangeEncryption;
    }

    public StorageFile File { get; set; }

    public ICommand CommandChangeEncryption { private get; set; }
    public ICommand CommandDecryptButton => new Cmd(() => CommandChangeEncryption.Execute(EncryptionMethods.None));
    public ICommand CommandCENCButton => new Cmd(() => CommandChangeEncryption.Execute(EncryptionMethods.CENC));
    public ICommand CommandDotnetButton => new Cmd(() => CommandChangeEncryption.Execute(EncryptionMethods.dotnet));
    public bool ShowDecryptButton => File.EncryptionMethod != EncryptionMethods.None && File.EncryptionMethod != EncryptionMethods.Unknown;
    public bool ShowCENCButton => File.EncryptionMethod != EncryptionMethods.CENC && File.EncryptionMethod != EncryptionMethods.Unknown && File.IsVideo;
    public bool ShowDotnetButton => File.EncryptionMethod != EncryptionMethods.dotnet && File.EncryptionMethod != EncryptionMethods.Unknown;
}