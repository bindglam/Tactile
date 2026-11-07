namespace Tactile.Core.Primitives;

/// <summary>3차원 벡터. 3D로 그리는 요소(손 등)의 좌표에 사용합니다.</summary>
public readonly struct Vector3(float x, float y, float z) : IEquatable<Vector3>
{
    public float X { get; } = x;
    public float Y { get; } = y;
    public float Z { get; } = z;

    public static Vector3 Zero => new(0f, 0f, 0f);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 v, float scale) => new(v.X * scale, v.Y * scale, v.Z * scale);

    public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

    public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public override int GetHashCode() => (((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397) ^ Z.GetHashCode();
    public override string ToString() => $"({X}, {Y}, {Z})";

    public static bool operator ==(Vector3 left, Vector3 right) => left.Equals(right);
    public static bool operator !=(Vector3 left, Vector3 right) => !left.Equals(right);
}
