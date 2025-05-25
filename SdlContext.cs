using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace TheAdventure;

public class SdlContext : INativeContext
{
    private readonly IntPtr _nativeLibrary;

    public SdlContext()
    {
        string runtimesPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "runtimes");

        string libraryName;
        string platform;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            libraryName = "libSDL2-2.0.so";
            platform = "linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            libraryName = "SDL2.dll";
            platform = "win";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var local = Path.Combine(runtimesPath, "osx", "native", "libSDL2-2.0.0.dylib");
            if (File.Exists(local))
            {
                // if you still ship the runtime, use it
                _nativeLibrary = NativeLibrary.Load(local);
            }
            else
            {
                // otherwise let dyld find the Homebrew‐installed one
                _nativeLibrary = NativeLibrary.Load("libSDL2-2.0.dylib");
            }
            return;
        }
        else
        {
            throw new PlatformNotSupportedException("Only Linux, macOS, and Windows are supported.");
        }

        if (RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            _nativeLibrary = NativeLibrary.Load(Path.Combine(runtimesPath, platform + "-x64", "native", libraryName));
        }
        else if (RuntimeInformation.OSArchitecture == Architecture.X86)
        {
            _nativeLibrary = NativeLibrary.Load(Path.Combine(runtimesPath, platform + "-x86", "native", libraryName));
        }
        else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new PlatformNotSupportedException("ARM64 is not supported on Linux.");
            }

            _nativeLibrary = NativeLibrary.Load(Path.Combine(runtimesPath, platform + "-arm", "native", libraryName));
        }
        else
        {
            throw new PlatformNotSupportedException("Only x64, x86, and ARM64 are supported.");
        }
    }

    public IntPtr GetProcAddress(string proc, int? slot = null)
    {
        return NativeLibrary.GetExport(_nativeLibrary, proc);
    }

    public bool TryGetProcAddress(string proc, [UnscopedRef] out IntPtr addr, int? slot = null)
    {
        try
        {
            addr = NativeLibrary.GetExport(_nativeLibrary, proc);
        }
        catch (EntryPointNotFoundException)
        {
            addr = IntPtr.Zero;
        }

        return addr != IntPtr.Zero;
    }

    private void ReleaseUnmanagedResources()
    {
        NativeLibrary.Free(_nativeLibrary);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~SdlContext()
    {
        ReleaseUnmanagedResources();
    }
}

