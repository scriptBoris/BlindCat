using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.Tools;
using BlindCatCore.ViewModels;

namespace BlindCatAvalonia;

public partial class DirPresentView : Grid
{
    public DirPresentView()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            string path = @"C:\Media\Images\Mems";
            var vm = new DirPresentVm(new DirPresentVm.Key
            {
                DirectoryPath = path
            }, new DesignStorageService(), null)
            {
                NavigationService = null,
                ViewModelResolver = null,
                ViewPlatforms = null,
            };
            vm.Dir.FilesSource = new(DirPresentVm.GetFiles(path, vm.Dir));
            DataContext = vm;
        }
        //imgPrev.SetSource(@"C:\Media\Images\Mems\15484325893040.jpg");
    }
}