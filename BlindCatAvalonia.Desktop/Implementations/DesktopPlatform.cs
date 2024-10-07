using Avalonia.Controls.ApplicationLifetimes;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.Desktop.Utils;
using BlindCatAvalonia.Tools;
using BlindCatCore.Core;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Desktop.Implementations;

public class DesktopPlatform : AvaloniaPlatform
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

        //if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classic)
        //{
        //    return classic.MainWindow!.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        //    {
        //        AllowMultiple = false,
        //        Title = "Select folder",
        //    }).ContinueWith(x =>
        //    {
        //        var r = x.Result;
        //        return x.Result.FirstOrDefault()?.Path.LocalPath;
        //    });
        //}
        //else
        //{
        //    throw new NotSupportedException();
        //}
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

    public override AppResponse ShowFileOnExplorer(string filePath)
    {
        if (!File.Exists(filePath))
            return AppResponse.Error($"Не удалось найти файл \"{filePath}\"");

        // Открытие папки в Проводнике Windows
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = true
        });
        return AppResponse.OK;
    }
}