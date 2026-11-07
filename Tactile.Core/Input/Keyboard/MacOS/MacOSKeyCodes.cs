using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Tactile.Core.Input.Keyboard.MacOS;

/// <summary>
/// macOS 가상 키코드(Carbon HIToolbox/Events.h의 kVK_*)와 <see cref="KeyCode"/> 사이의 변환.
/// <para>
/// macOS 키코드는 물리적 위치 기준이므로, 글자/숫자/기호 키는 현재 키보드 배열(TIS)로
/// 실제 찍히는 글자를 구해서 변환합니다. 예: AZERTY에서 kVK_ANSI_Q 위치의 키 → <see cref="KeyCode.A"/>.
/// 한글 등 비라틴 입력기를 쓰는 중이면 함께 설정된 라틴 배열(ABC, Dvorak 등)을 기준으로 합니다.
/// </para>
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacOSKeyCodes
{
    private static readonly KeyCode[] PositionalTable = BuildPositionalTable();

    /// <summary>
    /// macOS 가상 키코드를 공용 키코드로 변환합니다. 모르는 키는 <see cref="KeyCode.Unknown"/>.
    /// </summary>
    public static KeyCode ToKeyCode(int macKeyCode)
    {
        if ((uint)macKeyCode >= (uint)PositionalTable.Length)
            return KeyCode.Unknown;

        KeyCode positional = PositionalTable[macKeyCode];
        if (!IsCharacterKey(positional))
            return positional;

        // 글자 키: 현재 배열 기준으로 미리 만들어 둔 변환표에서 조회
        return KeyboardLayout.GetTable()[macKeyCode];
    }

    /// <summary>
    /// 공용 키코드를 현재 키보드 배열 기준 macOS 가상 키코드로 변환합니다. 대응하는 키가 없으면 false.
    /// </summary>
    public static bool TryFromKeyCode(KeyCode key, out int macKeyCode)
    {
        KeyCode[] table = KeyboardLayout.GetTable();
        for (int code = 0; code < table.Length; code++)
        {
            if (table[code] != KeyCode.Unknown && table[code] == key)
            {
                macKeyCode = code;
                return true;
            }
        }

        macKeyCode = -1;
        return false;
    }

    /// <summary>
    /// 키보드 배열 변환표를 즉시 다시 만듭니다. 변환표는 입력 소스가 바뀌었는지 최대 1초 간격으로
    /// 자동 확인하므로 보통은 호출할 필요가 없고, 배열 변경을 즉시 반영해야 할 때만 사용하세요.
    /// </summary>
    public static void RefreshLayout() => KeyboardLayout.Invalidate();

    /// <summary>키보드 배열에 따라 찍히는 글자가 달라지는 키인지.</summary>
    private static bool IsCharacterKey(KeyCode key) => key switch
    {
        >= KeyCode.A and <= KeyCode.D0 => true,
        >= KeyCode.Minus and <= KeyCode.Slash => true,
        KeyCode.NonUsBackslash or KeyCode.IntlYen or KeyCode.IntlRo => true,
        _ => false,
    };

    private static KeyCode? FromCharacters(char normal, char shifted)
    {
        if (normal is >= 'a' and <= 'z')
            return (KeyCode)((int)KeyCode.A + (normal - 'a'));
        if (normal is >= 'A' and <= 'Z')
            return (KeyCode)((int)KeyCode.A + (normal - 'A'));

        // AZERTY처럼 숫자가 Shift 쪽에 있는 배열도 숫자 키로 취급
        if (DigitToKeyCode(normal) is { } digit)
            return digit;
        if (DigitToKeyCode(shifted) is { } shiftedDigit)
            return shiftedDigit;

        return normal switch
        {
            '-' => KeyCode.Minus,
            '=' => KeyCode.Equal,
            '[' => KeyCode.LeftBracket,
            ']' => KeyCode.RightBracket,
            '\\' => KeyCode.Backslash,
            ';' => KeyCode.Semicolon,
            '\'' => KeyCode.Quote,
            '`' => KeyCode.Grave,
            ',' => KeyCode.Comma,
            '.' => KeyCode.Period,
            '/' => KeyCode.Slash,
            '¥' => KeyCode.IntlYen,
            _ => null,
        };
    }

    private static KeyCode? DigitToKeyCode(char c) => c switch
    {
        '0' => KeyCode.D0,
        >= '1' and <= '9' => (KeyCode)((int)KeyCode.D1 + (c - '1')),
        _ => null,
    };

    /// <summary>위치 기준 기본 표. 글자 키는 배열 해석에 실패했을 때만 이 값을 사용합니다.</summary>
    private static KeyCode[] BuildPositionalTable()
    {
        var t = new KeyCode[0x80];

        t[0x00] = KeyCode.A;
        t[0x01] = KeyCode.S;
        t[0x02] = KeyCode.D;
        t[0x03] = KeyCode.F;
        t[0x04] = KeyCode.H;
        t[0x05] = KeyCode.G;
        t[0x06] = KeyCode.Z;
        t[0x07] = KeyCode.X;
        t[0x08] = KeyCode.C;
        t[0x09] = KeyCode.V;
        t[0x0A] = KeyCode.NonUsBackslash;   // kVK_ISO_Section
        t[0x0B] = KeyCode.B;
        t[0x0C] = KeyCode.Q;
        t[0x0D] = KeyCode.W;
        t[0x0E] = KeyCode.E;
        t[0x0F] = KeyCode.R;
        t[0x10] = KeyCode.Y;
        t[0x11] = KeyCode.T;
        t[0x12] = KeyCode.D1;
        t[0x13] = KeyCode.D2;
        t[0x14] = KeyCode.D3;
        t[0x15] = KeyCode.D4;
        t[0x16] = KeyCode.D6;
        t[0x17] = KeyCode.D5;
        t[0x18] = KeyCode.Equal;
        t[0x19] = KeyCode.D9;
        t[0x1A] = KeyCode.D7;
        t[0x1B] = KeyCode.Minus;
        t[0x1C] = KeyCode.D8;
        t[0x1D] = KeyCode.D0;
        t[0x1E] = KeyCode.RightBracket;
        t[0x1F] = KeyCode.O;
        t[0x20] = KeyCode.U;
        t[0x21] = KeyCode.LeftBracket;
        t[0x22] = KeyCode.I;
        t[0x23] = KeyCode.P;
        t[0x24] = KeyCode.Enter;            // kVK_Return
        t[0x25] = KeyCode.L;
        t[0x26] = KeyCode.J;
        t[0x27] = KeyCode.Quote;
        t[0x28] = KeyCode.K;
        t[0x29] = KeyCode.Semicolon;
        t[0x2A] = KeyCode.Backslash;
        t[0x2B] = KeyCode.Comma;
        t[0x2C] = KeyCode.Slash;
        t[0x2D] = KeyCode.N;
        t[0x2E] = KeyCode.M;
        t[0x2F] = KeyCode.Period;
        t[0x30] = KeyCode.Tab;
        t[0x31] = KeyCode.Space;
        t[0x32] = KeyCode.Grave;
        t[0x33] = KeyCode.Backspace;        // kVK_Delete
        t[0x35] = KeyCode.Escape;
        t[0x36] = KeyCode.RightMeta;        // kVK_RightCommand
        t[0x37] = KeyCode.LeftMeta;         // kVK_Command
        t[0x38] = KeyCode.LeftShift;
        t[0x39] = KeyCode.CapsLock;
        t[0x3A] = KeyCode.LeftAlt;          // kVK_Option
        t[0x3B] = KeyCode.LeftControl;
        t[0x3C] = KeyCode.RightShift;
        t[0x3D] = KeyCode.RightAlt;         // kVK_RightOption
        t[0x3E] = KeyCode.RightControl;
        t[0x3F] = KeyCode.Function;
        t[0x40] = KeyCode.F17;
        t[0x41] = KeyCode.NumpadDecimal;
        t[0x43] = KeyCode.NumpadMultiply;
        t[0x45] = KeyCode.NumpadAdd;
        t[0x47] = KeyCode.NumLock;          // kVK_ANSI_KeypadClear
        t[0x48] = KeyCode.VolumeUp;
        t[0x49] = KeyCode.VolumeDown;
        t[0x4A] = KeyCode.Mute;
        t[0x4B] = KeyCode.NumpadDivide;
        t[0x4C] = KeyCode.NumpadEnter;
        t[0x4E] = KeyCode.NumpadSubtract;
        t[0x4F] = KeyCode.F18;
        t[0x50] = KeyCode.F19;
        t[0x51] = KeyCode.NumpadEqual;
        t[0x52] = KeyCode.Numpad0;
        t[0x53] = KeyCode.Numpad1;
        t[0x54] = KeyCode.Numpad2;
        t[0x55] = KeyCode.Numpad3;
        t[0x56] = KeyCode.Numpad4;
        t[0x57] = KeyCode.Numpad5;
        t[0x58] = KeyCode.Numpad6;
        t[0x59] = KeyCode.Numpad7;
        t[0x5A] = KeyCode.F20;
        t[0x5B] = KeyCode.Numpad8;
        t[0x5C] = KeyCode.Numpad9;
        t[0x5D] = KeyCode.IntlYen;          // kVK_JIS_Yen
        t[0x5E] = KeyCode.IntlRo;           // kVK_JIS_Underscore
        t[0x5F] = KeyCode.NumpadComma;      // kVK_JIS_KeypadComma
        t[0x60] = KeyCode.F5;
        t[0x61] = KeyCode.F6;
        t[0x62] = KeyCode.F7;
        t[0x63] = KeyCode.F3;
        t[0x64] = KeyCode.F8;
        t[0x65] = KeyCode.F9;
        t[0x66] = KeyCode.Lang2;            // kVK_JIS_Eisu
        t[0x67] = KeyCode.F11;
        t[0x68] = KeyCode.Lang1;            // kVK_JIS_Kana
        t[0x69] = KeyCode.F13;
        t[0x6A] = KeyCode.F16;
        t[0x6B] = KeyCode.F14;
        t[0x6D] = KeyCode.F10;
        t[0x6E] = KeyCode.ContextMenu;      // kVK_ContextualMenu
        t[0x6F] = KeyCode.F12;
        t[0x71] = KeyCode.F15;
        t[0x72] = KeyCode.Insert;           // kVK_Help (PC 키보드의 Insert가 이 코드로 들어옴)
        t[0x73] = KeyCode.Home;
        t[0x74] = KeyCode.PageUp;
        t[0x75] = KeyCode.Delete;           // kVK_ForwardDelete
        t[0x76] = KeyCode.F4;
        t[0x77] = KeyCode.End;
        t[0x78] = KeyCode.F2;
        t[0x79] = KeyCode.PageDown;
        t[0x7A] = KeyCode.F1;
        t[0x7B] = KeyCode.Left;
        t[0x7C] = KeyCode.Right;
        t[0x7D] = KeyCode.Down;
        t[0x7E] = KeyCode.Up;

        return t;
    }

    /// <summary>
    /// 현재 키보드 배열(Text Input Source) 기준 macOS 키코드 → <see cref="KeyCode"/> 변환표를 캐시합니다.
    /// <para>
    /// 입력 소스 조회(약 14µs)를 키 입력마다 하지 않도록, 변환표를 한 번 만들어 두고
    /// 최대 1초(<see cref="CheckIntervalTicks"/>) 간격으로만 입력 소스가 바뀌었는지 확인합니다.
    /// </para>
    /// </summary>
    private static class KeyboardLayout
    {
        private static readonly long CheckIntervalTicks = Stopwatch.Frequency; // 1초
        private static readonly object Gate = new();

        private static KeyCode[]? _table;
        private static IntPtr _source;      // _table을 만든 입력 소스 (retain 상태)
        private static long _lastCheck;

        public static KeyCode[] GetTable()
        {
            lock (Gate)
            {
                long now = Stopwatch.GetTimestamp();
                if (_table is not null && now - _lastCheck < CheckIntervalTicks)
                    return _table;

                // GUI 앱에서 TIS를 메인 스레드 밖에서 부르면 메인 스레드를 기다리며 멈출 수 있음 (실측 확인).
                // 메인 스레드가 아니면 TIS를 부르지 않고, 만들어 둔 표가 없으면 위치 기준 표를 사용
                if (!MacOSNative.IsMainThread)
                    return _table ?? PositionalTable;

                _lastCheck = now;

                // 한글 등 입력기가 켜져 있어도 라틴 배열 기준으로 해석
                IntPtr source = TISCopyCurrentASCIICapableKeyboardLayoutInputSource();
                if (source == IntPtr.Zero)
                    return _table ??= (KeyCode[])PositionalTable.Clone();

                if (_table is not null && _source != IntPtr.Zero && CFEqual(source, _source))
                {
                    CFRelease(source); // 입력 소스 그대로 → 기존 변환표 사용
                    return _table;
                }

                KeyCode[] table = Build(source);
                if (_source != IntPtr.Zero)
                    CFRelease(_source);
                _source = source;
                _table = table;
                return table;
            }
        }

        public static void Invalidate()
        {
            lock (Gate)
            {
                _table = null;
            }
        }

        private static KeyCode[] Build(IntPtr source)
        {
            var table = (KeyCode[])PositionalTable.Clone();

            IntPtr data = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData);
            if (data == IntPtr.Zero)
                return table;

            IntPtr layout = CFDataGetBytePtr(data);
            uint keyboardType = LMGetKbdType();

            for (int code = 0; code < table.Length; code++)
            {
                if (!IsCharacterKey(PositionalTable[code]))
                    continue;

                char normal = Translate(layout, code, 0, keyboardType);
                char shifted = Translate(layout, code, ShiftModifier, keyboardType);

                // 해석할 수 없는 글자(é, § 등)면 위치 기준 값을 그대로 둠
                if (FromCharacters(normal, shifted) is { } key)
                    table[code] = key;
            }

            return table;
        }

        private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const ushort kUCKeyActionDisplay = 3;
        private const uint kUCKeyTranslateNoDeadKeysMask = 1;
        private const uint ShiftModifier = 0x02; // (shiftKey >> 8) & 0xFF

        private static readonly IntPtr kTISPropertyUnicodeKeyLayoutData =
            MacOSNative.LoadConstant(Carbon, "kTISPropertyUnicodeKeyLayoutData");

        private static char Translate(IntPtr layout, int macKeyCode, uint modifiers, uint keyboardType)
        {
            uint deadKeyState = 0;
            var buffer = new char[4];
            int status = UCKeyTranslate(
                layout, (ushort)macKeyCode, kUCKeyActionDisplay, modifiers, keyboardType,
                kUCKeyTranslateNoDeadKeysMask, ref deadKeyState, (nuint)buffer.Length, out nuint length, buffer);

            return status == 0 && length > 0 ? buffer[0] : '\0';
        }

        [DllImport(Carbon)]
        private static extern IntPtr TISCopyCurrentASCIICapableKeyboardLayoutInputSource();

        [DllImport(Carbon)]
        private static extern IntPtr TISGetInputSourceProperty(IntPtr source, IntPtr key);

        [DllImport(Carbon)]
        private static extern byte LMGetKbdType();

        [DllImport(Carbon)]
        private static extern int UCKeyTranslate(
            IntPtr keyLayoutPtr, ushort virtualKeyCode, ushort keyAction, uint modifierKeyState,
            uint keyboardType, uint keyTranslateOptions, ref uint deadKeyState,
            nuint maxStringLength, out nuint actualStringLength, [Out] char[] unicodeString);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDataGetBytePtr(IntPtr data);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFEqual(IntPtr cf1, IntPtr cf2);
    }
}
