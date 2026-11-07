namespace Tactile.Core.Input.Keyboard;

/// <summary>
/// 운영체제와 무관한 공용 키코드입니다.
/// <para>
/// 글자/숫자/기호 키는 물리적 위치가 아니라 <b>현재 키보드 배열에서 찍히는 글자</b>를 기준으로 합니다.
/// 예를 들어 AZERTY 배열에서 왼쪽 위 글자 키는 <see cref="A"/>, Dvorak에서 QWERTY의 S 위치 키는 <see cref="O"/>입니다.
/// 숫자가 Shift 쪽에 있는 배열(AZERTY 상단열 등)도 숫자 키로 취급합니다.
/// 한글 등 비라틴 입력기를 쓰는 중에는 함께 설정된 라틴 배열을 기준으로 합니다.
/// </para>
/// <para>
/// 글자와 무관한 키(F1, 방향키, 수정자, 숫자 키패드 등)는 배열과 상관없이 고정입니다.
/// 배열 기준으로 해석할 수 없는 글자(é, § 등)를 내는 키는 위치 기준 값으로 대체됩니다.
/// </para>
/// <para>
/// 숫자 값 자체는 USB HID Usage ID를 차용했을 뿐이며, 의미는 위의 글자 기준을 따릅니다.
/// HID에 없는 키(<see cref="Function"/> 등)는 0x100 이상 값을 사용합니다.
/// </para>
/// </summary>
public enum KeyCode : ushort
{
    Unknown = 0x00,

    // 알파벳
    A = 0x04, B = 0x05, C = 0x06, D = 0x07, E = 0x08, F = 0x09, G = 0x0A,
    H = 0x0B, I = 0x0C, J = 0x0D, K = 0x0E, L = 0x0F, M = 0x10, N = 0x11,
    O = 0x12, P = 0x13, Q = 0x14, R = 0x15, S = 0x16, T = 0x17, U = 0x18,
    V = 0x19, W = 0x1A, X = 0x1B, Y = 0x1C, Z = 0x1D,

    // 숫자 (상단 숫자열)
    D1 = 0x1E, D2 = 0x1F, D3 = 0x20, D4 = 0x21, D5 = 0x22,
    D6 = 0x23, D7 = 0x24, D8 = 0x25, D9 = 0x26, D0 = 0x27,

    Enter = 0x28,
    Escape = 0x29,
    Backspace = 0x2A,
    Tab = 0x2B,
    Space = 0x2C,

    Minus = 0x2D,           // - _
    Equal = 0x2E,           // = +
    LeftBracket = 0x2F,     // [ {
    RightBracket = 0x30,    // ] }
    Backslash = 0x31,       // \ |
    NonUsHash = 0x32,       // ISO 배열의 Enter 왼쪽 키
    Semicolon = 0x33,       // ; :
    Quote = 0x34,           // ' "
    Grave = 0x35,           // ` ~
    Comma = 0x36,           // , <
    Period = 0x37,          // . >
    Slash = 0x38,           // / ?

    CapsLock = 0x39,

    F1 = 0x3A, F2 = 0x3B, F3 = 0x3C, F4 = 0x3D, F5 = 0x3E, F6 = 0x3F,
    F7 = 0x40, F8 = 0x41, F9 = 0x42, F10 = 0x43, F11 = 0x44, F12 = 0x45,

    PrintScreen = 0x46,
    ScrollLock = 0x47,
    Pause = 0x48,
    Insert = 0x49,          // Mac 키보드의 Help 키
    Home = 0x4A,
    PageUp = 0x4B,
    Delete = 0x4C,          // Forward Delete
    End = 0x4D,
    PageDown = 0x4E,

    Right = 0x4F,
    Left = 0x50,
    Down = 0x51,
    Up = 0x52,

    // 숫자 키패드
    NumLock = 0x53,         // Mac 키보드의 Clear 키
    NumpadDivide = 0x54,
    NumpadMultiply = 0x55,
    NumpadSubtract = 0x56,
    NumpadAdd = 0x57,
    NumpadEnter = 0x58,
    Numpad1 = 0x59, Numpad2 = 0x5A, Numpad3 = 0x5B, Numpad4 = 0x5C, Numpad5 = 0x5D,
    Numpad6 = 0x5E, Numpad7 = 0x5F, Numpad8 = 0x60, Numpad9 = 0x61, Numpad0 = 0x62,
    NumpadDecimal = 0x63,

    NonUsBackslash = 0x64,  // ISO 배열의 왼쪽 Shift 오른쪽 키 (Mac의 § 키)
    ContextMenu = 0x65,     // Windows 메뉴(Application) 키
    Power = 0x66,
    NumpadEqual = 0x67,

    F13 = 0x68, F14 = 0x69, F15 = 0x6A, F16 = 0x6B, F17 = 0x6C, F18 = 0x6D,
    F19 = 0x6E, F20 = 0x6F, F21 = 0x70, F22 = 0x71, F23 = 0x72, F24 = 0x73,

    Help = 0x75,

    Mute = 0x7F,
    VolumeUp = 0x80,
    VolumeDown = 0x81,

    NumpadComma = 0x85,     // JIS 키패드 쉼표
    IntlRo = 0x87,          // JIS ろ / _
    KanaMode = 0x88,        // JIS かな (PC)
    IntlYen = 0x89,         // JIS ¥
    Convert = 0x8A,         // JIS 変換
    NonConvert = 0x8B,      // JIS 無変換
    Lang1 = 0x90,           // 한/영 키, Mac의 かな 키
    Lang2 = 0x91,           // 한자 키, Mac의 英数 키

    // 수정자 키 (Alt = Mac의 Option, Meta = Windows 키 / Mac의 Command)
    LeftControl = 0xE0,
    LeftShift = 0xE1,
    LeftAlt = 0xE2,
    LeftMeta = 0xE3,
    RightControl = 0xE4,
    RightShift = 0xE5,
    RightAlt = 0xE6,
    RightMeta = 0xE7,

    // HID 표준에 없는 키
    Function = 0x100,       // Fn (OS에 보고되는 경우에만)
}
