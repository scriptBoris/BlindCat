using ScaffoldLib.Maui.Core;

namespace BlindCatMaui.Views.Popups;

public partial class EditTagsPopup : IAppear
{
	public EditTagsPopup()
	{
		InitializeComponent();
	}

    public void OnAppear(bool isComplete)
    {
		if (isComplete)
		{
            autoSuggest.Focus();
        }
    }
}