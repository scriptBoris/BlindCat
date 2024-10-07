using Microsoft.UI.Xaml.Controls;

namespace BlindCatMaui.Platforms.Windows.Native;

public partial class PromtPasswordDialog : ContentDialog
{
    public PromtPasswordDialog(string title, string description, string ok, string cancel, string placeholder)
    {
        InitializeComponent();
        Title = title;
        textBody.Text = description;
        PrimaryButtonText = ok;
        SecondaryButtonText = cancel;
        this.KeyDown += PromtPasswordDialog_KeyDown;
        passwordBox.Focus(Microsoft.UI.Xaml.FocusState.Pointer);
    }

    private void PromtPasswordDialog_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            IsTapeOK = true;
            this.Hide();
        }
    }

    /// <summary>
    /// ебаный костыль майкрософт
    /// </summary>
    public bool IsTapeOK { get; private set; }
    public string Password => passwordBox.Password;
}