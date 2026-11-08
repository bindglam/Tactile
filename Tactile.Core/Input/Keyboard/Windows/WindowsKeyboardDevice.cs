using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Tactile.Core.Input.Keyboard.Windows.WindowsNative;

namespace Tactile.Core.Input.Keyboard.Windows;

/// <summary>
/// 저수준 키보드 훅(<c>WH_KEYBOARD_LL</c>)으로 전역 키보드 입력을 수신합니다. 다른 창에 포커스가 있어도 받습니다.
/// <para>
/// Raw Input은 프로세스마다 키보드 수신 창을 하나만 등록할 수 있어서, 게임 안(모드)에서 쓰면 게임의 입력 등록과 서로 빼앗게 됩니다.
/// 훅은 여러 개가 함께 있어도 서로 영향을 주지 않으므로 이쪽을 씁니다.
/// 훅 콜백은 수집 스레드의 메시지 루프에서 불리며, 큐에 넣기만 하고 바로 다음 훅으로 넘깁니다 (입력을 막거나 바꾸지 않음).
/// </para>
/// <para>
/// 별도 권한은 필요 없지만, 관리자 권한으로 실행한 게임에 포커스가 있을 때는 Windows가 입력을 넘겨주지 않습니다
/// (이때는 키뷰어도 관리자 권한으로 실행).
/// </para>
/// <para>
/// <see cref="KeyboardEvent.RawCode"/>는 스캔 코드(set 1)입니다. 확장 키는 0xE000을 더한 값(예: 오른쪽 Ctrl = 0xE01D),
/// Pause는 0xE11D. <see cref="KeyboardEvent.Key"/>는 위치 기준 공용 키코드이고, 글자 키만 현재 키보드 배열을 따릅니다
/// (AZERTY의 Q 자리 키 → <see cref="KeyCode.A"/>). 키를 누르고 있을 때 반복되는 Down은 버립니다.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsKeyboardDevice : KeyboardDevice
{
    // IL2CPP는 네이티브로 넘기는 콜백이 static이어야 함. 델리게이트는 GC되지 않게 static 필드에 보관.
    // 훅 콜백은 훅을 건 스레드(수집 스레드)에서만 불리므로, 그 스레드의 장치를 ThreadStatic으로 찾음
    private static readonly LowLevelKeyboardProc s_callback = OnHookStatic;

    [ThreadStatic]
    private static WindowsKeyboardDevice? t_current;

    // 수집 스레드에서만 사용 (_threadId는 RequestStopCapture에서도 읽음. Start가 초기화 완료를 기다리므로 값이 보장됨)
    private readonly HashSet<int> _pressed = new();
    private uint _threadId;
    private IntPtr _hook;

    /// <param name="maxPendingEvents">큐에 쌓아 둘 최대 이벤트 수. 넘치면 가장 오래된 것부터 버립니다.</param>
    public WindowsKeyboardDevice(int maxPendingEvents = DefaultMaxPendingEvents)
        : base(maxPendingEvents)
    {
        if (!IsWindows)
            throw new PlatformNotSupportedException("WindowsKeyboardDevice는 Windows에서만 사용할 수 있습니다.");
    }

    protected override void InitializeCapture()
    {
        _threadId = GetCurrentThreadId();
        _pressed.Clear();
        t_current = this;

        // 메시지 큐를 미리 만들어 둠 (PostThreadMessage로 보낸 중지 요청이 사라지지 않게)
        PeekMessageW(out _, IntPtr.Zero, 0, 0, 0);

        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, s_callback, GetModuleHandleW(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException($"키보드 훅을 설치하지 못했습니다 (오류 {Marshal.GetLastWin32Error()}).");
    }

    protected override void RunCapture()
    {
        // 저수준 훅은 훅을 건 스레드가 메시지를 처리하는 동안 불림
        while (!IsStopRequested)
        {
            if (GetMessageW(out _, IntPtr.Zero, 0, 0) <= 0)
                break; // WM_QUIT(0) 또는 오류(-1)
        }
    }

    protected override void RequestStopCapture()
    {
        if (_threadId != 0)
            PostThreadMessageW(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    protected override void CleanupCapture()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        if (t_current == this)
            t_current = null;
        _pressed.Clear();
    }

    /// <summary>스캔 코드를 키코드로 바꿉니다. 글자 키는 지금 앞에 있는 창의 키보드 배열을 따릅니다.</summary>
    protected override KeyCode ResolveKey(int rawCode)
    {
        KeyCode positional = WindowsScanCodes.ToKeyCode(rawCode);

        IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        if (layout == IntPtr.Zero)
            layout = GetKeyboardLayout(0);

        uint vk = MapVirtualKeyExW((uint)rawCode, MAPVK_VSC_TO_VK_EX, layout);
        if (vk is >= 'A' and <= 'Z')
            return (KeyCode)((int)KeyCode.A + (int)(vk - 'A'));
        if (vk == VK_HANGUL)
            return KeyCode.Lang1;
        if (vk == VK_HANJA)
            return KeyCode.Lang2;
        return positional;
    }

    /// <summary>가상 키 → 스캔 코드. 잠금 키처럼 변환이 안 되는 환경(Wine 등)이 있어 몇 개는 직접 채웁니다.</summary>
    private static int ScanCodeOf(int virtualKey)
    {
        int scanCode = (int)MapVirtualKeyExW((uint)virtualKey, MAPVK_VK_TO_VSC_EX, IntPtr.Zero);
        if (scanCode != 0)
            return scanCode;
        return virtualKey switch
        {
            0x14 => 0x3A,   // VK_CAPITAL
            0x90 => 0x45,   // VK_NUMLOCK
            0x91 => 0x46,   // VK_SCROLL
            0x2C => 0xE037, // VK_SNAPSHOT
            _ => 0,
        };
    }

    [AOT.MonoPInvokeCallback(typeof(LowLevelKeyboardProc))]
    private static IntPtr OnHookStatic(int code, IntPtr wParam, IntPtr lParam)
    {
        // 예외가 네이티브 경계를 넘으면 프로세스가 죽으므로 전부 잡음
        try
        {
            if (code == HC_ACTION && t_current is { } device)
                device.OnKey((int)wParam.ToInt64(), lParam);
        }
        catch
        {
            // 입력 하나를 놓치는 것이 낫다
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private void OnKey(int message, IntPtr lParam)
    {
        long timestamp = Stopwatch.GetTimestamp();
        if (message is not (WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP))
            return;

        // KBDLLHOOKSTRUCT: vkCode, scanCode, flags (각 uint). 할당 없이 직접 읽음
        int virtualKey = Marshal.ReadInt32(lParam, 0);
        int scanCode = Marshal.ReadInt32(lParam, 4) & 0xFF;
        uint flags = (uint)Marshal.ReadInt32(lParam, 8);
        bool extended = (flags & LLKHF_EXTENDED) != 0;

        if (virtualKey == VK_PAUSE)
            scanCode = 0xE11D; // Pause는 장치마다 스캔 코드가 다르게 와서 가상 키로 판별
        else if (virtualKey == VK_RSHIFT)
            scanCode = 0x36;
        else if (virtualKey == VK_LSHIFT && extended)
            return; // NumLock이 켜진 상태에서 방향키 등에 딸려 오는 가짜 Shift
        else if (scanCode == 0)
            scanCode = ScanCodeOf(virtualKey); // 스캔 코드 없이 가상 키만 보낸 입력 (매크로, 원격 입력 등)
        else if (extended)
            scanCode |= 0xE000;

        if (scanCode == 0 || WindowsScanCodes.IsFakeShift(scanCode))
            return;

        if ((flags & LLKHF_UP) != 0)
        {
            if (_pressed.Remove(scanCode))
                Enqueue(scanCode, KeyAction.Up, timestamp);
        }
        else if (_pressed.Add(scanCode)) // 누르고 있을 때의 자동 반복은 버림
        {
            Enqueue(scanCode, KeyAction.Down, timestamp);
        }
    }
}
