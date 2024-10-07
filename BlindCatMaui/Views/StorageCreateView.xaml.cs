using BlindCatCore.Services;
using BlindCatMaui.Core;

namespace BlindCatMaui.Views;

public partial class StorageCreateView : IDisposable
{
	public StorageCreateView()
	{
		InitializeComponent();
        buttonSelectDir.Clicked += Button_Clicked;
    }

    public void Dispose()
    {
        buttonSelectDir.Clicked -= Button_Clicked;
    }

    private async void Button_Clicked(object? sender, EventArgs e)
    {
		string? dir = await this.DiFetch<IViewPlatforms>().SelectDirectory(this);
		if (dir == null)
			return;

		entryPath.Text = dir;
    }
}