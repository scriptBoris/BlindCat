using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.SDcontrols;
using BlindCatAvalonia.SDcontrols.Scaffold.Args;
using BlindCatCore.Core;
using System;
using System.Reflection.Metadata;
using System.Windows.Input;

namespace BlindCatAvalonia.SDcontrols.Scaffold.Utils;

public partial class NavBar : Grid
{
    private readonly Control _content;
    private readonly ScaffoldView _scaffoldView;
    private readonly Agent _agent;
    private Control? _customNavbar;

    public const double NAVBARHEIGHT = 45;

#pragma warning disable CS8618 
    [Obsolete("ONLY FOR DESIGN TIME")]
    public NavBar()
    {
        InitializeComponent();
        Height = NAVBARHEIGHT;
        titleLabel.Text = "TEST TITLE";
        menuItems.ItemsSource = new AvaloniaList<ScaffoldMenu>
        {
            new ScaffoldMenu
            {
                Text = "Item 1",
            },
            new ScaffoldMenu
            {
                Text = "Settings",
            },
            new ScaffoldMenu
            {
                Text = "Clear",
            },
        };
    }
#pragma warning restore CS8618 

    public NavBar(Control content, AgentArgs args)
    {
        _content = content;
        _scaffoldView = args.ScaffoldView;
        _agent = args.Agent;
        CommandMenuItem = new Cmd<MenuItem>(ActionMenuItem);
        InitializeComponent();

        Height = NAVBARHEIGHT;
        backButton.Command = new Cmd(ActionBackButton);

        if (!UpdateHasNavBar())
            return;

        if (UpdateCustomNavigationBar())
            return;

        UpdateBackground();
        UpdateTitle();
        UpdateMenuItems();

        if (args.HideBackButton)
        {
            backButton.IsVisible = false;
        }
    }

    #region commands
    public ICommand CommandMenuItem { get; }
    private void ActionMenuItem(MenuItem menuItem)
    {
        menuItem.Command?.Execute(null);
    }
    #endregion commands

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _content.PropertyChanged += Content_PropertyChanged;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _content.PropertyChanged -= Content_PropertyChanged;
    }

    private void Content_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Scaffolt.TitleProperty)
        {
            UpdateTitle();
        }
        else if (e.Property == Scaffolt.CustomNavigationBarProperty)
        {
            UpdateCustomNavigationBar();
        }
        else if (e.Property == Scaffolt.SubtitleProperty)
        {
            UpdateTitle();
        }
    }

    private void UpdateBackground()
    {
        var bg = Scaffolt.GetBackgroundNavigationBar(_content);
        this.Background = bg;
    }

    private bool UpdateHasNavBar()
    {
        bool res = Scaffolt.GetHasNavigationBar(_content);
        return res;
    }

    public void UpdateTitle()
    {
        string? title = Scaffolt.GetTitle(_content);
        titleLabel.Text = title;

        string? subtitle = Scaffolt.GetSubtitle(_content);
        subtitleLabel.Text = subtitle;
        subtitleLabel.IsVisible = subtitle != null;
    }

    public void UpdateMenuItems()
    {
        menuItems.ItemsSource = Scaffolt.GetMenuItems(_content);
    }

    public bool UpdateCustomNavigationBar()
    {
        var old = _customNavbar;
        var nev = Scaffolt.GetCustomNavigationBar(_content);

        // deatach
        if (old != null && old != nev)
        {
            old.PropertyChanged -= CustomNavBar_PropertyChanged;
            Children.Remove(old);
        }

        // attach
        if (nev != null && old != nev)
        {
            nev.DataContext = _content.DataContext;
            Children.Add(nev);
            Grid.SetColumnSpan(nev, 3);
            nev.PropertyChanged += CustomNavBar_PropertyChanged;
        }

        bool has = nev != null && nev.IsVisible;
        if (has)
        {
            UpdateUpdCustomNavbar(true);
        }
        _customNavbar = nev;
        return has;
    }

    private void CustomNavBar_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
        {
            bool flag = (bool)e.NewValue!;
            UpdateUpdCustomNavbar(flag);
        }
    }

    private void UpdateUpdCustomNavbar(bool show)
    {
        backButton.IsVisible = !show;
        titleLabel.IsVisible = !show;
        menuItems.IsVisible = !show;
        subtitleLabel.IsVisible = !show;
    }

    private void ActionBackButton()
    {
        _scaffoldView.OnBackButton(_agent);
    }
}