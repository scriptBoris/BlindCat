using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.Tools;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;

namespace BlindCatAvalonia.Views.Popups;

public partial class SaveFilesView : Grid
{
    public SaveFilesView()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            var storage = new StorageDir
            {
                Name = "Design",
                Path = "C:/data/storage_test",
            };
            var file = new StorageFile
            {
                IsTemp = true,
                Storage = storage,
                FilePath = "C:/data/test.jpeg",
                Tags = ["cat", "animal", "nature"],
                Name = "funny cat",
                Description = "ƒобавление в ресурсный словарь: ≈сли ты хочешь использовать и модифицировать этот стиль, ты можешь добавить его в свой файл стилей, например.",
                Artist = "Boris Kit",
            };
            DataContext = new SaveFilesVm(new SaveFilesVm.Key
            {
                SaveFiles = [file],
                StorageDir = storage,
            }, new DesignStorageService(), null)
            {
                NavigationService = null,
                ViewModelResolver = null,
                ViewPlatforms = null,
            };
        }
    }
}