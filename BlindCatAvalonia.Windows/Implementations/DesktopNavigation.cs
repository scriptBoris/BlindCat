﻿using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.SDcontrols;
using BlindCatAvalonia.Services;
using BlindCatAvalonia.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Windows.Implementations;

public class DesktopNavigation : NavigationService
{
    public override async Task<object?> Popup(object view, object? viewFor)
    {
        return await WindowsManager.MakeWindow(view, viewFor);
    }

    public override Task PopupClose(object view)
    {
        var v = (Control)view;
        var p = v.GetVisualRoot() as Window;
        p.Close();
        return Task.CompletedTask;
    }
}