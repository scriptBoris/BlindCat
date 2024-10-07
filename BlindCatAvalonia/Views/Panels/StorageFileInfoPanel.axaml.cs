using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Models;
using BlindCatCore.ViewModels.Panels;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace BlindCatAvalonia.Views.Panels;

public partial class StorageFileInfoPanel : Grid
{
    [Obsolete("Design time")]
    public StorageFileInfoPanel()
    {
        InitializeComponent();
        //DataContext = new StorageFileInfoPanelVm
        //{
        //    Name = "simple_picture",
        //    Description = "ƒобавление в ресурсный словарь: ≈сли ты хочешь использовать и модифицировать этот стиль, ты можешь добавить его в свой файл стилей, например.",
        //    Artist = "test artist",
        //    //Tags = [],
        //    Tags = ["tag1", "cat", "brain zombie!", "compik", "call of duti"],
        //    EncryptionMethod = EncryptionMethods.dotnet,
        //    Format = MediaFormats.Jpeg,
        //    CommandChangeEncryption = null,
        //};
    }

    public StorageFileInfoPanel(StorageFile file)
    {
        InitializeComponent();
    }
}