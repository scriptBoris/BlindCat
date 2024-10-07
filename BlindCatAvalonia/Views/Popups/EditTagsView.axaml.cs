using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.Tools;
using BlindCatCore.Models;
using BlindCatCore.PopupViewModels;

namespace BlindCatAvalonia;

public partial class EditTagsView : Grid
{
    public EditTagsView()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            var dir = new StorageDir
            {
                Name = "Design storage",
                Path = "C:/data",
            };
            var file = new StorageFile
            {
                FilePath = "C:/data/test.jpeg",
                Storage = dir,
            };
            DataContext = new EditTagsVm(new EditTagsVm.Key
            {
                AlreadyTags = 
                [
                    new TagCount 
                    {
                        Count = 1,
                        TagName = "Cat",
                    },
                    new TagCount
                    {
                        Count = 1,
                        TagName = "Nature",
                    },
                    new TagCount
                    {
                        Count = 1,
                        TagName = "Animals",
                    },
                ] ,
                SelectedFiles = [file],
                StorageDir = dir,
            }, new DesignStorageService())
            {
                NavigationService = null,
                ViewModelResolver = null,
                ViewPlatforms = null,
            };
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        autoSuggestion.Focus();
    }
}