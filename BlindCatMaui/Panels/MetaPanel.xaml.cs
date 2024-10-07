using BlindCatCore.Core;

namespace BlindCatMaui.Panels;

public partial class MetaPanel
{
    private StorageFileController _controller;

    public MetaPanel(StorageFileController controller, string? title)
	{
		InitializeComponent();
        BindingContext = controller;
        _controller = controller;

        if (title != null)
        {
            labelTitle.Text = title;
            labelTitle.IsVisible = true;
        }
    }

	public Action? OnSave { get; set; }

    public void ChangeController(StorageFileController newest)
    {
        BindingContext = newest;
        _controller = newest;
    }

    private async void Button_Clicked(object sender, EventArgs e)
    {
        if (_controller.CommandSave.IsRunning)
            return;

        await _controller.CommandSave.ExecuteAsync(null);
		OnSave?.Invoke();
    }
}