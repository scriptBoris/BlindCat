using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.Services;
using BlindCatAvalonia.Views;
using BlindCatAvalonia.Views.PopupsDesktop;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using DynamicData;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Tools;

public abstract class PlatformAvaloniaDesktop : PlatformAvalonia
{
    public override Task<string?> SelectDirectory(object? hostView)
    {
        return WindowsManager.HandleWindow<string?>(hostView, async (window) =>
        {
            var res = await window.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select folder",
            });

            return res?.FirstOrDefault()?.Path.LocalPath;
        });
    }

    public override Task<IFileResult?> SelectMediaFile(object? hostView)
    {
        throw new NotImplementedException();
    }

    public override async Task ShowDialog(string title, string body, string OK, object? hostView)
    {
        var dialogWindow = new AlertWindow(title, body, OK, null);
        await WindowsManager.MakeWindow(dialogWindow, hostView);
    }

    public override async Task<bool> ShowDialog(string title, string body, string OK, string cancel, object? hostView)
    {
        var dialogWindow = new AlertWindow(title, body, OK, cancel);
        await WindowsManager.MakeWindow(dialogWindow, hostView);
        return dialogWindow.Result;
    }

    public override async Task<string?> ShowDialogPromt(string title, string message, string OK, string cancel, string placeholder, string initValue, object? hostView)
    {
        var dialogWindow = new AlertPromtWindow(title, message, OK, cancel, placeholder, initValue, false);
        await WindowsManager.MakeWindow(dialogWindow, hostView);
        return dialogWindow.Result;
    }

    public override async Task<string?> ShowDialogPromtPassword(string title, string message, string OK, string cancel, string placeholder, object? hostView)
    {

        var dialogWindow = new AlertPromtWindow(title, message, OK, cancel, placeholder, "", true);
        await WindowsManager.MakeWindow(dialogWindow, hostView);
        return dialogWindow.Result;
    }

    public override async Task<string?> ShowDialogSheet(string title, string cancel, string[] items, object? hostView)
    {
        var dialogWindow = new DialogSheetWindow(title, cancel, items);
        await WindowsManager.MakeWindow(dialogWindow, hostView);
        return dialogWindow.ResultString;
    }

    public override async Task<int?> ShowDialogSheetId(string title, string cancel, string[] items, object? hostView)
    {
        var dialogWindow = new DialogSheetWindow(title, cancel, items);
        await WindowsManager.MakeWindow(dialogWindow, hostView);
        return dialogWindow.ResultInt;
    }
}