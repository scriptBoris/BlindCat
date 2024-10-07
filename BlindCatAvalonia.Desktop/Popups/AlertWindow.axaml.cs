using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;

namespace BlindCatAvalonia.Desktop;

public partial class AlertWindow : Window
{
    [Obsolete("ONLY FOR DESIGNER")]
    public AlertWindow()
    {
        InitializeComponent();
    }

    public AlertWindow(string title, string body, string oK, string? cancel)
    {
        InitializeComponent();
        Title = title;
        labelTitle.Text = title;
        labelBody.Text = body;
        buttonOK.Content = oK;
        buttonOK.Click += ButtonOK_Click;
        buttonCancel.Content = cancel;
        buttonCancel.Click += ButtonCancel_Click;

        if (cancel == null)
        {
            buttonCancel.IsVisible = false;
            Grid.SetColumn(buttonOK, 2);
        }
    }

    private void ButtonOK_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void ButtonCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Enter:
                Result = true;
                Close();
                break;
            case Key.Escape:
                Result = false;
                Close();
                break;
            default:
                break;
        }
    }

    public bool Result { get; private set; }
}