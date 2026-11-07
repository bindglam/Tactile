using System.Globalization;

namespace Tactile.Core.Primitives;

/// <summary>RGBA 색상. 각 채널은 0~1 범위입니다.</summary>
public readonly struct Color(float r, float g, float b, float a = 1f) : IEquatable<Color>
{
    public float R { get; } = r;
    public float G { get; } = g;
    public float B { get; } = b;
    public float A { get; } = a;

    public static Color Transparent => new(0f, 0f, 0f, 0f);
    public static Color White => new(1f, 1f, 1f);
    public static Color Black => new(0f, 0f, 0f);

    /// <summary>0~255 채널 값으로 만듭니다.</summary>
    public static Color FromRgba(byte r, byte g, byte b, byte a = 255) =>
        new(r / 255f, g / 255f, b / 255f, a / 255f);

    /// <summary><c>#RRGGBB</c> 또는 <c>#RRGGBBAA</c> 형식(# 생략 가능)을 읽습니다.</summary>
    public static Color FromHex(string hex)
    {
        if (hex is null)
            throw new ArgumentNullException(nameof(hex));

        string s = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
        if (s.Length != 6 && s.Length != 8)
            throw new FormatException($"색상 형식이 올바르지 않습니다: {hex}");

        byte Channel(int index) => byte.Parse(s.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return FromRgba(Channel(0), Channel(1), Channel(2), s.Length == 8 ? Channel(3) : (byte)255);
    }

    /// <summary>두 색을 t(0~1) 비율로 섞습니다.</summary>
    public static Color Lerp(Color from, Color to, float t) => new(
        from.R + (to.R - from.R) * t,
        from.G + (to.G - from.G) * t,
        from.B + (to.B - from.B) * t,
        from.A + (to.A - from.A) * t);

    public bool Equals(Color other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = R.GetHashCode();
            hash = (hash * 397) ^ G.GetHashCode();
            hash = (hash * 397) ^ B.GetHashCode();
            return (hash * 397) ^ A.GetHashCode();
        }
    }

    public override string ToString() => $"RGBA({R:0.###}, {G:0.###}, {B:0.###}, {A:0.###})";

    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);
}
