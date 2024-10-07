using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.Core;
using BlindCatCore.Services;

namespace BlindCatAvalonia.Views;

public partial class StorageCreateView : Grid
{
    public StorageCreateView()
    {
        InitializeComponent();
    }

    private async void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dir = await this.DI<IViewPlatforms>().SelectDirectory(this);
        if (dir != null)
        {
            entryPath.Text = dir;
        }
    }
}