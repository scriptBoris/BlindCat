using Avalonia.Controls.ApplicationLifetimes;
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

namespace BlindCatAvalonia.Linux.Implementations;

public class DesktopPlatform : PlatformAvaloniaDesktop
{
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