using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatCore.Core;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace BlindCatAvalonia.Desktop;

public partial class DialogSheetWindow : Window
{
    private List<Item> itemsList;

    [Obsolete("Designer")]
    public DialogSheetWindow()
    {
        InitializeComponent();
        itemsControl.ItemsSource = new string[]
        {
            "helo",
            "cats",
            "dogs",
            "Исключение: При отмене задачи выбрасывается OperationCanceledException",
        };
    }

    public DialogSheetWindow(string title, string cancel, params string[] items)
    {
        InitializeComponent();
        Title = title;
        labelTitle.Text = title;
        buttonCancel.Content = cancel;

        itemsList = new List<Item>();
        for (int i = 0; i < items.Length; i++)
        {
            itemsList.Add(new Item
            {
                Index = i,
                Title = items[i],
            });
        }

        itemsControl.ItemsSource = itemsList;
        CommandClickItem = new Cmd<Item>(ActionClickItem);
    }

    public int? ResultInt { get; private set; }
    public string? ResultString { get; private set; }
    public ICommand CommandClickItem { get; private set; }

    private void ActionClickItem(Item item)
    {
        ResultInt = item.Index;
        ResultString = item.Title;
        Close();
    }

    private class Item
    {
        public required string Title { get; set; }
        public required int Index { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }
}