using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.Tools;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using BlindCatCore.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            DataContext = new HomeVm(new HomeVm.Key { },
            null, null, new DesignStorageService(), null, null)
            {
                NavigationService = null,
                ViewModelResolver = null,
                ViewPlatforms = null,
            };
        }
    }
}