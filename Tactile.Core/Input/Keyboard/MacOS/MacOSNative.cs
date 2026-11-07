using System.Runtime.InteropServices;

namespace Tactile.Core.Input.Keyboard.MacOS;

/// <summary>macOS 네이티브 공용 도우미. (netstandard2.0에는 NativeLibrary가 없어 dlopen/dlsym을 직접 사용)</summary>
internal static class MacOSNative
{
    private const string LibSystem = "/usr/lib/libSystem.dylib";
    private const int RTLD_LAZY = 0x1;

    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>현재 스레드가 프로세스의 메인 스레드인지.</summary>
    public static bool IsMainThread => pthread_main_np() != 0;

    /// <summary>프레임워크가 내보내는 전역 상수(<c>extern const CFStringRef</c> 등)의 값을 읽습니다.</summary>
    public static IntPtr LoadConstant(string library, string symbol)
    {
        IntPtr handle = dlopen(library, RTLD_LAZY);
        if (handle == IntPtr.Zero)
            throw new DllNotFoundException($"{library}를 열 수 없습니다.");

        IntPtr address = dlsym(handle, symbol);
        if (address == IntPtr.Zero)
            throw new EntryPointNotFoundException($"{library}에서 {symbol}을 찾을 수 없습니다.");

        return Marshal.ReadIntPtr(address);
    }

    [DllImport(LibSystem)]
    private static extern int pthread_main_np();

    [DllImport(LibSystem)]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport(LibSystem)]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);
}
