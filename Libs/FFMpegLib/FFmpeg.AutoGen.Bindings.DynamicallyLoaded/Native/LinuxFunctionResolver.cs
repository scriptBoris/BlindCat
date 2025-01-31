using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FFmpeg.AutoGen.Bindings.DynamicallyLoaded.Native;

public class LinuxFunctionResolver : FunctionResolverBase
{
    private const string Libdl = "libdl.so.2";

    private const int RTLD_NOW = 0x002;

    protected override string GetNativeLibraryName(string libraryName, int version) => $"lib{libraryName}.so.{version}";

    protected override IntPtr LoadNativeLibrary(string libraryName)
    {
        // clear previous errors if any
        dlerror();
        
        var pointer = dlopen(libraryName, RTLD_NOW);
        var errPtr = dlerror();
        if (errPtr != IntPtr.Zero)
        {
            string error = Marshal.PtrToStringAnsi(errPtr);
            Debug.WriteLine($"Failed to load native library: {error}");
        }

        return pointer;
    }

    protected override IntPtr GetFunctionPointer(IntPtr nativeLibraryHandle, string functionName) => dlsym(nativeLibraryHandle, functionName);

    [DllImport(Libdl)]
    public static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport(Libdl)]
    public static extern IntPtr dlopen(string fileName, int flag);
    
    [DllImport(Libdl)]
    private static extern IntPtr dlerror();
}
