namespace Tactile.Core.Input.Keyboard;

public enum KeyAction
{
    Down,
    Up
}

/// <param name="Key">운영체제와 무관한 공용 키코드</param>
/// <param name="RawCode">
/// 운영체제가 보고한 원래 키코드 (macOS: kVK_*, Windows: 스캔 코드 set 1 — 확장 키는 0xE000을 더함, Linux: evdev).
/// <see cref="Key"/>가 <see cref="KeyCode.Unknown"/>일 때 키를 구분하는 용도로 사용합니다.
/// </param>
public readonly record struct KeyboardEvent(
    KeyCode Key,
    int RawCode,
    KeyAction Action,
    long Timestamp) : IInputEvent;
