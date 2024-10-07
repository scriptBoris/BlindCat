namespace BlindCatMaui.SDControls;

public partial class LoadingInit
{
	public LoadingInit()
	{
		InitializeComponent();
	}

	public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
		nameof(IsLoading),
		typeof(bool),
		typeof(LoadingInit),
		true,
		propertyChanged: (b, o, n) =>
		{
			if (b is LoadingInit self)
				self.Update((bool)n);
		});
	public bool IsLoading
	{
		get => (bool)GetValue(IsLoadingProperty);
		set => SetValue(IsLoadingProperty, value);
	}

	private async void Update(bool show)
	{
		if (show)
		{
			//IsVisible = true;
			//Opacity = 1;
		}
		else
		{
            await this.FadeTo(0, 190);
            IsVisible = false;
		}
	}
}