namespace Tactile.Core.Primitives;

/// <summary>
/// 2차원 벡터 (레이아웃 픽셀 단위). 위치와 크기 모두에 사용합니다.
/// 원점은 왼쪽 위이고 y는 아래로 증가합니다.
/// </summary>
public readonly struct Vector2(float x, float y) : IEquatable<Vector2>
{
    public float X { get; } = x;
    public float Y { get; } = y;

    public static Vector2 Zero => new(0f, 0f);
    public static Vector2 One => new(1f, 1f);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, float scale) => new(v.X * scale, v.Y * scale);
    public static Vector2 operator *(float scale, Vector2 v) => new(v.X * scale, v.Y * scale);

    public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
    public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);
    public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);
}
