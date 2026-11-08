using System.Runtime.Versioning;

namespace Tactile.Core.Input.Keyboard.Windows;

/// <summary>
/// Windows 스캔 코드(set 1) → 공용 키코드(USB HID 위치 기준).
/// 확장 키는 0xE0xx(예: 오른쪽 Ctrl = 0xE01D), Pause는 0xE11D로 씁니다.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsScanCodes
{
    private static readonly KeyCode[] Normal = new KeyCode[256];
    private static readonly KeyCode[] Extended = new KeyCode[256];

    static WindowsScanCodes()
    {
        void N(int code, KeyCode key) => Normal[code] = key;
        void E(int code, KeyCode key) => Extended[code] = key;

        N(0x01, KeyCode.Escape);
        for (int i = 0; i < 9; i++)
            N(0x02 + i, (KeyCode)((int)KeyCode.D1 + i));
        N(0x0B, KeyCode.D0);
        N(0x0C, KeyCode.Minus);
        N(0x0D, KeyCode.Equal);
        N(0x0E, KeyCode.Backspace);
        N(0x0F, KeyCode.Tab);

        KeyCode[] top = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P };
        for (int i = 0; i < top.Length; i++)
            N(0x10 + i, top[i]);
        N(0x1A, KeyCode.LeftBracket);
        N(0x1B, KeyCode.RightBracket);
        N(0x1C, KeyCode.Enter);
        N(0x1D, KeyCode.LeftControl);

        KeyCode[] home = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L };
        for (int i = 0; i < home.Length; i++)
            N(0x1E + i, home[i]);
        N(0x27, KeyCode.Semicolon);
        N(0x28, KeyCode.Quote);
        N(0x29, KeyCode.Grave);
        N(0x2A, KeyCode.LeftShift);
        N(0x2B, KeyCode.Backslash); // ISO 배열의 Enter 왼쪽 키도 같은 코드

        KeyCode[] bottom = { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M };
        for (int i = 0; i < bottom.Length; i++)
            N(0x2C + i, bottom[i]);
        N(0x33, KeyCode.Comma);
        N(0x34, KeyCode.Period);
        N(0x35, KeyCode.Slash);
        N(0x36, KeyCode.RightShift);
        N(0x37, KeyCode.NumpadMultiply);
        N(0x38, KeyCode.LeftAlt);
        N(0x39, KeyCode.Space);
        N(0x3A, KeyCode.CapsLock);
        for (int i = 0; i < 10; i++)
            N(0x3B + i, (KeyCode)((int)KeyCode.F1 + i));
        N(0x45, KeyCode.NumLock);
        N(0x46, KeyCode.ScrollLock);

        // 숫자 키패드 (NumLock이 꺼져 있어도 위치는 같음)
        N(0x47, KeyCode.Numpad7);
        N(0x48, KeyCode.Numpad8);
        N(0x49, KeyCode.Numpad9);
        N(0x4A, KeyCode.NumpadSubtract);
        N(0x4B, KeyCode.Numpad4);
        N(0x4C, KeyCode.Numpad5);
        N(0x4D, KeyCode.Numpad6);
        N(0x4E, KeyCode.NumpadAdd);
        N(0x4F, KeyCode.Numpad1);
        N(0x50, KeyCode.Numpad2);
        N(0x51, KeyCode.Numpad3);
        N(0x52, KeyCode.Numpad0);
        N(0x53, KeyCode.NumpadDecimal);

        N(0x54, KeyCode.PrintScreen); // Alt+SysRq
        N(0x56, KeyCode.NonUsBackslash);
        N(0x57, KeyCode.F11);
        N(0x58, KeyCode.F12);
        N(0x59, KeyCode.NumpadEqual);
        for (int i = 0; i < 11; i++)
            N(0x64 + i, (KeyCode)((int)KeyCode.F13 + i)); // F13 ~ F23
        N(0x76, KeyCode.F24);

        // 한국어·일본어 키
        N(0x70, KeyCode.KanaMode);
        N(0x71, KeyCode.Lang2); // 한자
        N(0x72, KeyCode.Lang1); // 한/영
        N(0xF1, KeyCode.Lang2);
        N(0xF2, KeyCode.Lang1);
        N(0x73, KeyCode.IntlRo);
        N(0x79, KeyCode.Convert);
        N(0x7B, KeyCode.NonConvert);
        N(0x7D, KeyCode.IntlYen);
        N(0x7E, KeyCode.NumpadComma);

        // 0xE0 확장 키
        E(0x1C, KeyCode.NumpadEnter);
        E(0x1D, KeyCode.RightControl);
        E(0x20, KeyCode.Mute);
        E(0x2E, KeyCode.VolumeDown);
        E(0x30, KeyCode.VolumeUp);
        E(0x35, KeyCode.NumpadDivide);
        E(0x37, KeyCode.PrintScreen);
        E(0x38, KeyCode.RightAlt);
        E(0x45, KeyCode.NumLock);
        E(0x46, KeyCode.Pause); // Ctrl+Break
        E(0x47, KeyCode.Home);
        E(0x48, KeyCode.Up);
        E(0x49, KeyCode.PageUp);
        E(0x4B, KeyCode.Left);
        E(0x4D, KeyCode.Right);
        E(0x4F, KeyCode.End);
        E(0x50, KeyCode.Down);
        E(0x51, KeyCode.PageDown);
        E(0x52, KeyCode.Insert);
        E(0x53, KeyCode.Delete);
        E(0x5B, KeyCode.LeftMeta);
        E(0x5C, KeyCode.RightMeta);
        E(0x5D, KeyCode.ContextMenu);
        E(0x5E, KeyCode.Power);
        E(0xF1, KeyCode.Lang2);
        E(0xF2, KeyCode.Lang1);
    }

    /// <summary>확장 스캔 코드를 공용 키코드로 바꿉니다. 모르는 코드는 <see cref="KeyCode.Unknown"/>.</summary>
    public static KeyCode ToKeyCode(int scanCode)
    {
        int prefix = scanCode >> 8;
        int code = scanCode & 0xFF;
        return prefix switch
        {
            0 => Normal[code],
            0xE0 => Extended[code],
            0xE1 when code == 0x1D => KeyCode.Pause,
            _ => KeyCode.Unknown,
        };
    }

    /// <summary>
    /// 다른 키에 딸려 오는 가짜 Shift (NumLock이 켜진 상태에서 방향키·Home 등을 누를 때, PrintScreen 등).
    /// 실제로 누른 키가 아니므로 버립니다.
    /// </summary>
    public static bool IsFakeShift(int scanCode) => scanCode is 0xE02A or 0xE036;
}
