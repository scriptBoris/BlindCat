using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FFMpegDll.Models;

namespace FFMpegDll;

public static class Init
{
    private static object _lock = new();
    private static bool _initialized = false;

    public static void InitializeFFMpeg(ProcessorTypes? processorType = null)
    {
        if (_initialized)
            return;
        
        lock (_lock)
        {
            if (!_initialized)
            {
                _initialized = true;
                RegisterFFmpegBinaries(processorType);
                DynamicallyLoadedBindings.Initialize();
            }
        }
    }

    private static void RegisterFFmpegBinaries(ProcessorTypes? processorType = null)
    {
        string current = AppContext.BaseDirectory;
        string? librariesPath = null;
        bool useDirectDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string bitness = Environment.Is64BitProcess ? "x64" : "x86";
            string dirName = "win";
            useDirectDir = true;
            FunctionResolverFactory.ResolvedPlatform = PlatformTypes.Win32NT;
            librariesPath = Path.Combine(current, "FFmpeg", $"{dirName}-{bitness}");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string bitness = Environment.Is64BitProcess ? "x64" : "x86";
            string dirName = "linux";
            useDirectDir = false;
            FunctionResolverFactory.ResolvedPlatform = PlatformTypes.Unix;
            librariesPath = Path.Combine(current, "FFmpeg", $"{dirName}-{bitness}");
        }
        else if (OperatingSystem.IsAndroid())
        {
            string dirName = "droid";
            useDirectDir = false;
            string bitness = processorType.Value switch
            {
                ProcessorTypes.x86 => "x86",
                ProcessorTypes.x86_64 => "x64",
                ProcessorTypes.ARM64 => "arm64-v8a",
                _ => throw new InvalidOperationException("Not setuped processor type."),
            };
            FunctionResolverFactory.ResolvedPlatform = PlatformTypes.Android;
            librariesPath = Path.Combine(current, "FFmpeg", $"{dirName}-{bitness}");
        }
        else
        {
            throw new NotSupportedException(); // fell free add support for platform of your choose
        }

        if (!useDirectDir)
            return;
        
        if (Directory.Exists(librariesPath))
        {
            string[] files = Directory.GetFiles(librariesPath);
            DynamicallyLoadedBindings.LibrariesPath = librariesPath;
        }
        else
        {
            throw new InvalidDataException($"No match ffmpeg dll files by path: \"{librariesPath}\"");
        }
    }
}
