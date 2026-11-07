namespace Tactile.Core.Primitives;

/// <summary>
/// 사각형 영역 (레이아웃 픽셀 단위). 원점은 왼쪽 위이고 y는 아래로 증가합니다.
/// </summary>
public readonly struct Rect(float x, float y, float width, float height) : IEquatable<Rect>
{
    public Rect(Vector2 position, Vector2 size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    public float X { get; } = x;
    public float Y { get; } = y;
    public float Width { get; } = width;
    public float Height { get; } = height;

    public float Right => X + Width;
    public float Bottom => Y + Height;

    /// <summary>왼쪽 위 모서리.</summary>
    public Vector2 Position => new(X, Y);

    /// <summary>너비와 높이.</summary>
    public Vector2 Size => new(Width, Height);

    public bool Equals(Rect other) =>
        X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

    public override bool Equals(object? obj) => obj is Rect other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = X.GetHashCode();
            hash = (hash * 397) ^ Y.GetHashCode();
            hash = (hash * 397) ^ Width.GetHashCode();
            return (hash * 397) ^ Height.GetHashCode();
        }
    }

    public override string ToString() => $"({X}, {Y}, {Width}x{Height})";

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);
}
