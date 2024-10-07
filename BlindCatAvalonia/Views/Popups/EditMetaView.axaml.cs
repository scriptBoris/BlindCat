using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatCore.PopupViewModels;

namespace BlindCatAvalonia;

public partial class EditMetaView : Grid
{
    public EditMetaView()
    {
        InitializeComponent();
        
        if (Design.IsDesignMode)
        {
            DataContext = new EditMetaVm(new EditMetaVm.Key
            {
                File = new BlindCatCore.Models.StorageFile
                {
                    Storage = null,
                    FilePath = "C:/test/cute_cats.jpeg",
                    Name = "cute_cats",
                    CachedMediaFormat = BlindCatCore.Enums.MediaFormats.Jpeg,
                    Artist = "Major photographer",
                    Tags = ["cat", "jim goldi", "fork"],
                    Description = "ƒобавление в ресурсный словарь: ≈сли ты хочешь использовать и модифицировать этот стиль, ты можешь добавить его в свой файл стилей, например.",
                },
            }, null, null)
            {
                NavigationService = null,
                ViewModelResolver = null,
                ViewPlatforms = null,
            };
        }
    }
}