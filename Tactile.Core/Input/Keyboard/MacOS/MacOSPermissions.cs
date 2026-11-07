using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tactile.Core.Input.Keyboard.MacOS;

/// <summary>
/// macOS "입력 모니터링"(Input Monitoring) 권한.
/// <para>
/// CGPreflightListenEventAccess는 허용 여부만 알려 주지만, IOKit의 IOHIDCheckAccess는
/// "아직 묻지 않음"과 "거부됨"을 구분해 주므로 이쪽을 사용합니다. (macOS 10.15+)
/// </para>
/// </summary>
internal static class MacOSPermissions
{
    private const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";

    // IOHIDRequestType
    private const uint kIOHIDRequestTypeListenEvent = 1;

    // IOHIDAccessType
    private const uint kIOHIDAccessTypeGranted = 0;
    private const uint kIOHIDAccessTypeDenied = 1;

    private const string ListenEventSettingsUrl =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent";

    public static InputPermissionStatus CheckListenEvent() => IOHIDCheckAccess(kIOHIDRequestTypeListenEvent) switch
    {
        kIOHIDAccessTypeGranted => InputPermissionStatus.Granted,
        kIOHIDAccessTypeDenied => InputPermissionStatus.Denied,
        _ => InputPermissionStatus.NotDetermined,
    };

    public static InputPermissionStatus RequestListenEvent()
    {
        // 아직 묻지 않은 상태에서만 시스템 팝업이 뜨고, 바로 반환됨
        if (CheckListenEvent() == InputPermissionStatus.NotDetermined)
            IOHIDRequestAccess(kIOHIDRequestTypeListenEvent);

        return CheckListenEvent();
    }

    public static bool OpenListenEventSettings()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("/usr/bin/open", ListenEventSettingsUrl)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    [DllImport(IOKit)]
    private static extern uint IOHIDCheckAccess(uint requestType);

    [DllImport(IOKit)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool IOHIDRequestAccess(uint requestType);
}
