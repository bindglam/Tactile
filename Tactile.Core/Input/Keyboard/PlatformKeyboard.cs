using System.Runtime.InteropServices;
using Tactile.Core.Input.Keyboard.MacOS;
using Tactile.Core.Input.Keyboard.Windows;

namespace Tactile.Core.Input.Keyboard;

/// <summary>현재 운영체제의 전역 키보드 장치.</summary>
public static class PlatformKeyboard
{
    /// <summary>이 운영체제에 전역 키보드 장치가 있는지 (macOS, Windows).</summary>
    public static bool IsSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>현재 운영체제의 전역 키보드 장치를 만듭니다. 시작은 하지 않습니다.</summary>
    /// <exception cref="PlatformNotSupportedException">지원하지 않는 운영체제 (<see cref="IsSupported"/>가 false)</exception>
    public static KeyboardDevice Create(int maxPendingEvents = KeyboardDevice.DefaultMaxPendingEvents)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSKeyboardDevice(maxPendingEvents);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsKeyboardDevice(maxPendingEvents);
        throw new PlatformNotSupportedException($"이 운영체제({RuntimeInformation.OSDescription})는 아직 전역 키보드 입력을 지원하지 않습니다.");
    }
}
