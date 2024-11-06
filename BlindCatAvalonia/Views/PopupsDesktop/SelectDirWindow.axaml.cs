using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BlindCatAvalonia.Views.PopupsDesktop;

public partial class SelectDirWindow : Window
{
    public SelectDirWindow()
    {
        InitializeComponent();
    }

    public string? Path { get; private set; }
}