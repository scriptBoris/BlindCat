using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using BlindCatAvalonia.Core;
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

namespace BlindCatAvalonia.Windows.Implementations;

public class WindowsPlatform : PlatformAvaloniaDesktop
{
    public override IClipboard Clipboard { get; } = new WindowsClipboard();

    public override async Task<string?> SaveTo(string? defaultFileName, string? defaultDirectory)
    {
        var classic = (IClassicDesktopStyleApplicationLifetime)App.Current.ApplicationLifetime!;

        var opt = new FilePickerSaveOptions
        {
            SuggestedFileName = defaultFileName,
        };

        var f = await classic.MainWindow.StorageProvider.SaveFilePickerAsync(opt);
        if (f is null)
        {
            return null;
        }

        return f.Path.AbsolutePath;
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