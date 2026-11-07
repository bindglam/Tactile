using Tactile.Core.Input.Keyboard;

namespace Tactile.Core.Layout.Elements;

/// <summary>입력 장치 종류.</summary>
public enum InputSource
{
    Keyboard,
}

/// <summary>화면 요소에 연결할 입력 하나.</summary>
public readonly struct InputBinding(InputSource source, int code) : IEquatable<InputBinding>
{
    public InputSource Source { get; } = source;

    /// <summary>장치별 코드. <see cref="InputSource.Keyboard"/>면 <see cref="KeyCode"/> 값입니다.</summary>
    public int Code { get; } = code;

    public static InputBinding Keyboard(KeyCode key) => new(InputSource.Keyboard, (int)key);

    public bool Equals(InputBinding other) => Source == other.Source && Code == other.Code;
    public override bool Equals(object? obj) => obj is InputBinding other && Equals(other);
    public override int GetHashCode() => ((int)Source * 397) ^ Code;

    public override string ToString() => Source == InputSource.Keyboard ? $"Keyboard.{(KeyCode)Code}" : $"{Source}.{Code}";

    public static bool operator ==(InputBinding left, InputBinding right) => left.Equals(right);
    public static bool operator !=(InputBinding left, InputBinding right) => !left.Equals(right);
}
