using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Tactile.Core.Input;

namespace Tactile.Core.Input.Keyboard.MacOS;

/// <summary>
/// CGEventTap(Listen-only)으로 전역 키보드 입력을 수신합니다.
/// <para>
/// 실행하는 앱(터미널, IDE 등)에 "시스템 설정 → 개인정보 보호 및 보안 → 입력 모니터링" 권한이 필요합니다.
/// </para>
/// <para>
/// <see cref="KeyboardEvent.Key"/>는 공용 키코드로 변환된 값이고,
/// <see cref="KeyboardEvent.RawCode"/>는 macOS 가상 키코드(kVK_*)입니다.
/// 스레드 구조와 <see cref="KeyboardDevice.TryRead"/> 사용법은 <see cref="KeyboardDevice"/>를 참고하세요.
/// </para>
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSKeyboardDevice : KeyboardDevice
{
    // IL2CPP는 네이티브로 넘기는 콜백이 static이어야 하므로, 인스턴스는 GCHandle로 userInfo에 실어 보냄.
    // 델리게이트는 네이티브가 들고 있으므로 GC되지 않게 static 필드에 보관
    private static readonly Native.CGEventTapCallBack s_callback = OnEventStatic;

    private readonly HashSet<int> _pressedModifiers = [];
    private GCHandle _self;

    // 아래 필드는 수집 스레드에서만 쓰고, _runLoop만 RequestStopCapture에서 읽음
    // (Start가 InitializeCapture 완료를 기다린 뒤 반환하므로 값이 보장됨)
    private IntPtr _tap;
    private IntPtr _source;
    private IntPtr _runLoop;

    /// <param name="maxPendingEvents">큐에 쌓아 둘 최대 이벤트 수. 넘치면 가장 오래된 것부터 버립니다.</param>
    public MacOSKeyboardDevice(int maxPendingEvents = DefaultMaxPendingEvents)
        : base(maxPendingEvents)
    {
        if (!MacOSNative.IsMacOS)
            throw new PlatformNotSupportedException("MacOSKeyboardDevice는 macOS에서만 사용할 수 있습니다.");
    }

    protected override void InitializeCapture()
    {
        const ulong mask =
            (1UL << (int)Native.kCGEventKeyDown) |
            (1UL << (int)Native.kCGEventKeyUp) |
            (1UL << (int)Native.kCGEventFlagsChanged);

        _self = GCHandle.Alloc(this);
        _tap = Native.CGEventTapCreate(
            Native.kCGSessionEventTap,
            Native.kCGHeadInsertEventTap,
            Native.kCGEventTapOptionListenOnly,
            mask,
            s_callback,
            GCHandle.ToIntPtr(_self));

        if (_tap == IntPtr.Zero)
            throw new InvalidOperationException("CGEventTap을 생성하지 못했습니다. 입력 모니터링 권한을 확인하세요.");

        _source = Native.CFMachPortCreateRunLoopSource(IntPtr.Zero, _tap, 0);
        _runLoop = Native.CFRunLoopGetCurrent();
        Native.CFRunLoopAddSource(_runLoop, _source, Native.kCFRunLoopCommonModes);
        Native.CGEventTapEnable(_tap, true);
    }

    protected override void RunCapture()
    {
        // CFRunLoopStop은 런루프가 돌기 전에 호출되면 무시되므로, 짧은 타임아웃으로 돌면서
        // IsStopRequested를 다시 확인해 그 사이에 들어온 중지 요청도 놓치지 않게 함
        while (!IsStopRequested)
            Native.CFRunLoopRunInMode(Native.kCFRunLoopDefaultMode, 0.5, false);
    }

    protected override void RequestStopCapture()
    {
        if (_runLoop != IntPtr.Zero)
            Native.CFRunLoopStop(_runLoop); // 다른 스레드에서 호출해도 안전
    }

    protected override void CleanupCapture()
    {
        if (_source != IntPtr.Zero)
        {
            if (_runLoop != IntPtr.Zero)
                Native.CFRunLoopRemoveSource(_runLoop, _source, Native.kCFRunLoopCommonModes);
            Native.CFRelease(_source);
            _source = IntPtr.Zero;
        }

        if (_tap != IntPtr.Zero)
        {
            Native.CGEventTapEnable(_tap, false);
            Native.CFMachPortInvalidate(_tap);
            Native.CFRelease(_tap);
            _tap = IntPtr.Zero;
        }

        // 탭을 무효화한 뒤에는 콜백이 더 오지 않으므로 해제해도 안전
        if (_self.IsAllocated)
            _self.Free();

        _runLoop = IntPtr.Zero;
        _pressedModifiers.Clear();
    }

    protected override KeyCode ResolveKey(int rawCode) => MacOSKeyCodes.ToKeyCode(rawCode);

    protected override void PrepareStart()
    {
        // 아직 묻지 않은 상태면 시스템 팝업을 띄움 (거부된 상태에서는 팝업이 뜨지 않음)
        InputPermissionStatus permission = MacOSPermissions.RequestListenEvent();
        if (permission != InputPermissionStatus.Granted)
        {
            throw new UnauthorizedAccessException(permission == InputPermissionStatus.Denied
                ? "입력 모니터링 권한이 거부되어 있습니다. 시스템 설정 → 개인정보 보호 및 보안 → 입력 모니터링에서 허용하세요. (InputPermissions.OpenKeyboardSettings)"
                : "입력 모니터링 권한을 요청했습니다. 팝업에서 허용한 뒤 다시 시작하세요.");
        }

        // 키보드 배열 조회(TIS)는 메인 스레드에서만 안전하므로, 변환표를 여기서 미리 만들어 둠
        MacOSKeyCodes.ToKeyCode(0);
    }

    [AOT.MonoPInvokeCallback(typeof(Native.CGEventTapCallBack))]
    private static IntPtr OnEventStatic(IntPtr proxy, uint type, IntPtr cgEvent, IntPtr userInfo)
    {
        // 예외가 네이티브 경계를 넘으면 프로세스가 죽으므로 전부 잡는다
        try
        {
            if (userInfo != IntPtr.Zero && GCHandle.FromIntPtr(userInfo).Target is MacOSKeyboardDevice device)
                device.OnEvent(type, cgEvent);
        }
        catch
        {
            // ignored
        }

        return cgEvent;
    }

    private void OnEvent(uint type, IntPtr cgEvent)
    {
        switch (type)
        {
            case Native.kCGEventTapDisabledByTimeout:
            case Native.kCGEventTapDisabledByUserInput:
                if (_tap != IntPtr.Zero)
                    Native.CGEventTapEnable(_tap, true);
                break;

            case Native.kCGEventKeyDown:
                // 키를 누르고 있을 때 발생하는 자동 반복은 무시
                if (Native.CGEventGetIntegerValueField(cgEvent, Native.kCGKeyboardEventAutorepeat) != 0)
                    break;
                Enqueue(GetKeyCode(cgEvent), KeyAction.Down, GetEventTimestamp(cgEvent));
                break;

            case Native.kCGEventKeyUp:
                Enqueue(GetKeyCode(cgEvent), KeyAction.Up, GetEventTimestamp(cgEvent));
                break;

            case Native.kCGEventFlagsChanged:
                HandleFlagsChanged(cgEvent);
                break;
        }
    }

    /// <summary>
    /// 수정자 키(Shift, Control, Option, Command, Fn, Caps Lock)는 KeyDown/KeyUp 대신
    /// FlagsChanged로만 들어오므로 플래그를 보고 눌림/뗌을 판정합니다.
    /// </summary>
    private void HandleFlagsChanged(IntPtr cgEvent)
    {
        int code = GetKeyCode(cgEvent);
        ulong flags = Native.CGEventGetFlags(cgEvent);
        long timestamp = GetEventTimestamp(cgEvent);

        if (code == KeyCodes.CapsLock)
        {
            // Caps Lock은 플래그가 "잠금 상태"라 뗌을 알 수 없음 → 누를 때마다 Down/Up을 한 번에 보냄
            Enqueue(code, KeyAction.Down, timestamp);
            Enqueue(code, KeyAction.Up, timestamp);
            return;
        }

        bool isDown = ModifierMask(code) is { } modifierMask
            ? (flags & modifierMask) != 0
            : !_pressedModifiers.Contains(code); // 알 수 없는 수정자 키는 토글로 처리

        if (isDown == _pressedModifiers.Contains(code))
            return; // 상태 변화 없음 (반대편 Shift 등으로 인한 이벤트)

        if (isDown) _pressedModifiers.Add(code);
        else _pressedModifiers.Remove(code);

        Enqueue(code, isDown ? KeyAction.Down : KeyAction.Up, timestamp);
    }

    /// <summary>
    /// OS가 이벤트에 기록한 발생 시각을 <see cref="Stopwatch.GetTimestamp"/> 단위로 변환합니다.
    /// GC 일시정지나 스레드 스케줄링으로 콜백이 늦게 실행돼도 실제 입력 시각이 유지됩니다.
    /// <para>
    /// CGEventGetTimestamp는 문서상 나노초지만 Apple Silicon에서는 mach_absolute_time 틱으로 나올 수 있어,
    /// 두 해석 중 현재 시각에 맞는 쪽을 고릅니다. (macOS의 Stopwatch는 mach_absolute_time과 같은
    /// CLOCK_UPTIME_RAW 기준) 둘 다 맞지 않으면 현재 시각으로 대체합니다.
    /// </para>
    /// </summary>
    private static long GetEventTimestamp(IntPtr cgEvent)
    {
        long now = Stopwatch.GetTimestamp();
        ulong raw = Native.CGEventGetTimestamp(cgEvent);
        if (raw == 0)
            return now;

        long asNanoseconds = NanosecondsToStopwatch(raw);
        long asMachTicks = NanosecondsToStopwatch(MachTicksToNanoseconds(raw));

        long best = now;
        long bestAge = long.MaxValue;
        Consider(asNanoseconds);
        Consider(asMachTicks);
        return best;

        void Consider(long candidate)
        {
            long age = now - candidate;
            if (age >= -MaxFutureSkew && age <= MaxEventAge && Math.Abs(age) < bestAge)
            {
                best = Math.Min(candidate, now);
                bestAge = Math.Abs(age);
            }
        }
    }

    private static readonly long MaxEventAge = Stopwatch.Frequency;           // 1초 이상 지난 값이면 잘못 해석한 것으로 간주
    private static readonly long MaxFutureSkew = Stopwatch.Frequency / 1000;  // 1ms 정도의 반올림 오차 허용
    private static readonly Native.MachTimebaseInfo Timebase = Native.GetTimebase();

    private static ulong MachTicksToNanoseconds(ulong ticks) =>
        MultiplyDivide(ticks, Timebase.Numer, Timebase.Denom);

    // Stopwatch 단위는 런타임마다 다름 (CoreCLR: 1ns, Mono: 100ns)
    private static long NanosecondsToStopwatch(ulong nanoseconds) =>
        (long)MultiplyDivide(nanoseconds, (ulong)Stopwatch.Frequency, 1_000_000_000UL);

    /// <summary>value * numer / denom을 중간 오버플로 없이 계산합니다. (numer, denom ≤ 약 4e9 가정)</summary>
    private static ulong MultiplyDivide(ulong value, ulong numer, ulong denom) =>
        value / denom * numer + value % denom * numer / denom;

    private static int GetKeyCode(IntPtr cgEvent) =>
        (int)Native.CGEventGetIntegerValueField(cgEvent, Native.kCGKeyboardEventKeycode);

    /// <summary>좌/우를 구분하는 디바이스별 플래그 (IOKit NX_DEVICE*KEYMASK).</summary>
    private static ulong? ModifierMask(int code) => code switch
    {
        KeyCodes.LeftControl => 0x00000001,
        KeyCodes.LeftShift => 0x00000002,
        KeyCodes.RightShift => 0x00000004,
        KeyCodes.LeftCommand => 0x00000008,
        KeyCodes.RightCommand => 0x00000010,
        KeyCodes.LeftOption => 0x00000020,
        KeyCodes.RightOption => 0x00000040,
        KeyCodes.RightControl => 0x00002000,
        KeyCodes.Function => 0x00800000, // kCGEventFlagMaskSecondaryFn
        _ => null,
    };

    private static class KeyCodes
    {
        public const int RightCommand = 0x36;
        public const int LeftCommand = 0x37;
        public const int LeftShift = 0x38;
        public const int CapsLock = 0x39;
        public const int LeftOption = 0x3A;
        public const int LeftControl = 0x3B;
        public const int RightShift = 0x3C;
        public const int RightOption = 0x3D;
        public const int RightControl = 0x3E;
        public const int Function = 0x3F;
    }

    private static class Native
    {
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // CGEventTapLocation / Placement / Options
        public const uint kCGSessionEventTap = 1;
        public const uint kCGHeadInsertEventTap = 0;
        public const uint kCGEventTapOptionListenOnly = 1;

        // CGEventType
        public const uint kCGEventKeyDown = 10;
        public const uint kCGEventKeyUp = 11;
        public const uint kCGEventFlagsChanged = 12;
        public const uint kCGEventTapDisabledByTimeout = 0xFFFFFFFE;
        public const uint kCGEventTapDisabledByUserInput = 0xFFFFFFFF;

        // CGEventField
        public const int kCGKeyboardEventAutorepeat = 8;
        public const int kCGKeyboardEventKeycode = 9;

        public static readonly IntPtr kCFRunLoopCommonModes = MacOSNative.LoadConstant(CoreFoundation, "kCFRunLoopCommonModes");
        public static readonly IntPtr kCFRunLoopDefaultMode = MacOSNative.LoadConstant(CoreFoundation, "kCFRunLoopDefaultMode");

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr CGEventTapCallBack(IntPtr proxy, uint type, IntPtr cgEvent, IntPtr userInfo);

        [DllImport(CoreGraphics)]
        public static extern IntPtr CGEventTapCreate(
            uint tap, uint place, uint options, ulong eventsOfInterest,
            CGEventTapCallBack callback, IntPtr userInfo);

        [DllImport(CoreGraphics)]
        public static extern void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enable);

        [DllImport(CoreGraphics)]
        public static extern long CGEventGetIntegerValueField(IntPtr cgEvent, int field);

        [DllImport(CoreGraphics)]
        public static extern ulong CGEventGetFlags(IntPtr cgEvent);

        [DllImport(CoreGraphics)]
        public static extern ulong CGEventGetTimestamp(IntPtr cgEvent);

        [DllImport(CoreFoundation)]
        public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, nint order);

        [DllImport(CoreFoundation)]
        public static extern void CFMachPortInvalidate(IntPtr port);

        [DllImport(CoreFoundation)]
        public static extern IntPtr CFRunLoopGetCurrent();

        [DllImport(CoreFoundation)]
        public static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

        [DllImport(CoreFoundation)]
        public static extern void CFRunLoopRemoveSource(IntPtr runLoop, IntPtr source, IntPtr mode);

        [DllImport(CoreFoundation)]
        public static extern int CFRunLoopRunInMode(
            IntPtr mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSourceHandled);

        [DllImport(CoreFoundation)]
        public static extern void CFRunLoopStop(IntPtr runLoop);

        [DllImport(CoreFoundation)]
        public static extern void CFRelease(IntPtr cf);

        [StructLayout(LayoutKind.Sequential)]
        public struct MachTimebaseInfo
        {
            public uint Numer;
            public uint Denom;
        }

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern int mach_timebase_info(out MachTimebaseInfo info);

        public static MachTimebaseInfo GetTimebase()
        {
            // 실패하거나 0이 나오면 1:1(Intel Mac과 동일)로 간주
            if (mach_timebase_info(out MachTimebaseInfo info) != 0 || info.Numer == 0 || info.Denom == 0)
                return new MachTimebaseInfo { Numer = 1, Denom = 1 };
            return info;
        }
    }
}
