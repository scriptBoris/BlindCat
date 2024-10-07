using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.SDcontrols;
using BlindCatCore.Enums;
using System.Linq;

namespace BlindCatAvalonia;

public partial class StoragePresentView : Grid
{
    public StoragePresentView()
    {
        SourtingItems = new SortingStorageItems[]
        {
            SortingStorageItems.ByDateIndex,
            SortingStorageItems.Random,
        };

        InitializeComponent();
    }

    public SortingStorageItems[] SourtingItems { get; }
}