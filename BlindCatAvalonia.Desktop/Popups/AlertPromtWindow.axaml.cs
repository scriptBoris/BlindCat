using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace BlindCatAvalonia.Desktop;

public partial class AlertPromtWindow : Window
{
    [Obsolete("DESIGN")]
    public AlertPromtWindow()
    {
        InitializeComponent();
    }

    public AlertPromtWindow(string title, string message, string OK, string cancel, string placeholder, string initValue, bool isPassword)
    {
        InitializeComponent();
        Title = title;
        labelTitle.Text = title;
        labelBody.Text = message;
        buttonOK.Content = OK;
        buttonCancel.Content = cancel;
        entryValue.Text = initValue;
        entryValue.Watermark = placeholder;

        if (isPassword)
        {
            entryValue.PasswordChar = '*';
        }
    }

    public string? Result { get; private set; }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        entryValue.Focus();
        entryValue.KeyDown += EntryValue_KeyDown;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        entryValue.KeyDown -= EntryValue_KeyDown;
    }

    private void EntryValue_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && !string.IsNullOrEmpty(entryValue.Text))
        {
            Result = entryValue.Text;
            Close();
        }
    }

    private void Button_ClickCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void Button_ClickOK(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = entryValue.Text;
        Close();
    }
}