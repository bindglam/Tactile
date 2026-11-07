using System.Runtime.InteropServices;
using Tactile.Core.Input.Keyboard.MacOS;

namespace Tactile.Core.Input;

/// <summary>전역 입력을 읽는 권한의 상태.</summary>
public enum InputPermissionStatus
{
    /// <summary>허용됨.</summary>
    Granted,

    /// <summary>거부됨. 시스템 팝업이 다시 뜨지 않으므로 사용자가 직접 시스템 설정에서 허용해야 합니다.</summary>
    Denied,

    /// <summary>아직 묻지 않음. <see cref="InputPermissions.RequestKeyboard"/>로 시스템 팝업을 띄울 수 있습니다.</summary>
    NotDetermined,

    /// <summary>이 플랫폼에서는 별도의 권한이 필요 없음.</summary>
    NotRequired,
}

/// <summary>
/// 전역 입력 권한 확인·요청. 플랫폼별 차이를 숨깁니다.
/// <list type="bullet">
/// <item>macOS: "입력 모니터링" 권한이 필요합니다.</item>
/// <item>그 외 플랫폼: 현재는 <see cref="InputPermissionStatus.NotRequired"/>를 반환합니다.</item>
/// </list>
/// </summary>
public static class InputPermissions
{
    /// <summary>키보드 입력 권한의 현재 상태를 확인합니다. 팝업을 띄우지 않습니다.</summary>
    public static InputPermissionStatus CheckKeyboard() =>
        IsMacOS ? MacOSPermissions.CheckListenEvent() : InputPermissionStatus.NotRequired;

    /// <summary>
    /// 키보드 입력 권한을 요청합니다.
    /// <para>
    /// 상태가 <see cref="InputPermissionStatus.NotDetermined"/>일 때만 시스템 팝업이 뜹니다.
    /// 팝업은 바로 반환되며, 사용자의 선택은 나중에 <see cref="CheckKeyboard"/>로 확인하세요.
    /// 이미 거부된 경우에는 팝업이 뜨지 않으므로 <see cref="OpenKeyboardSettings"/>로 설정 화면을 열어 주세요.
    /// </para>
    /// </summary>
    /// <returns>요청 직후의 상태</returns>
    public static InputPermissionStatus RequestKeyboard() =>
        IsMacOS ? MacOSPermissions.RequestListenEvent() : InputPermissionStatus.NotRequired;

    /// <summary>
    /// 키보드 입력 권한을 바꿀 수 있는 시스템 설정 화면을 엽니다.
    /// </summary>
    /// <returns>열었으면 true, 이 플랫폼에 해당 화면이 없거나 열지 못했으면 false</returns>
    public static bool OpenKeyboardSettings() =>
        IsMacOS && MacOSPermissions.OpenListenEventSettings();

    private static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}
