using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FFMpegDll;

public static class Init
{
    private static bool initialized = false;

    public static void InitializeFFMpeg()
    {
        if (!initialized)
        {
            initialized = true;
            RegisterFFmpegBinaries();
            DynamicallyLoadedBindings.Initialize();
        }
    }

    private static void RegisterFFmpegBinaries()
    {
        string bitness = Environment.Is64BitProcess ? "x64" : "x86";
        string current = AppContext.BaseDirectory;
        string dir;
        bool useDirectDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            dir = "win";
            useDirectDir = true;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            dir = "linux";
            useDirectDir = false;
        }
        else
        {
            throw new NotSupportedException(); // fell free add support for platform of your choose
        }

        if (!useDirectDir)
            return;
        
        string probe = Path.Combine(current, "FFmpeg", $"{dir}-{bitness}");

        if (Directory.Exists(probe))
        {
            DynamicallyLoadedBindings.LibrariesPath = probe;
        }
        else
        {
            throw new InvalidDataException("No match ffmpeg dll files");
        }
    }
}
