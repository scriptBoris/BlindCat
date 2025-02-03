using System.Windows.Input;

namespace BlindCatMauiMobile.Controls;

public class Button : Microsoft.Maui.Controls.Button
{
    public Button()
    {
        Clicked += OnClicked;
    }

    ~Button()
    {
        Clicked -= OnClicked;
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            Command?.Execute(CommandParameter);
        });
    }

    public new static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(Button), 
        null
    );
    public new ICommand? Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}