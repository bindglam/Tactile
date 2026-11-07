using Tactile.Adapters.Unity.Utils;
using Tactile.Core.Layout;
using UnityEngine;
using CoreRect = Tactile.Core.Primitives.Rect;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>
/// 요소의 배경 상자. <see cref="BoxStyle.SpriteKey"/>가 있으면 이미지, 없으면 테두리가 있는 둥근 사각형으로 그립니다.
/// 값이 바뀐 프레임에만 메시를 다시 만듭니다.
/// </summary>
internal sealed class BoxView : IDisposable
{
    private const int CornerSegments = 6;

    private readonly MeshView _mesh;
    private readonly Func<string, Texture?>? _textureResolver;
    private bool _hasLast;
    private CoreRect _lastBounds;
    private BoxStyle _last;

    public BoxView(Transform parent, Func<string, Texture?>? textureResolver)
    {
        _mesh = new MeshView("Box", parent);
        _textureResolver = textureResolver;
    }

    public void Apply(CoreRect bounds, in BoxStyle look)
    {
        if (_hasLast && _lastBounds == bounds && SameLook(_last, look))
            return;

        _lastBounds = bounds;
        _last = look;
        _hasLast = true;

        _mesh.Clear();
        if (look.SpriteKey is not null)
        {
            _mesh.SetTexture(_textureResolver?.Invoke(look.SpriteKey));
            _mesh.Quad(bounds.X, bounds.Y, bounds.Width, bounds.Height, look.Fill.ToUnity32());
        }
        else
        {
            _mesh.SetTexture(null);
            float radius = Math.Max(0f, Math.Min(look.CornerRadius, Math.Min(bounds.Width, bounds.Height) / 2f));
            float border = Math.Max(0f, Math.Min(look.BorderWidth, Math.Min(bounds.Width, bounds.Height) / 2f));

            if (border > 0f && look.Border.A > 0f)
            {
                RoundedRing(bounds, radius, border, look.Border.ToUnity32());
                RoundedFill(Inset(bounds, border), Math.Max(0f, radius - border), look.Fill.ToUnity32());
            }
            else
            {
                RoundedFill(bounds, radius, look.Fill.ToUnity32());
            }
        }

        _mesh.Commit();
    }

    public void Dispose() => _mesh.Dispose();

    // BoxStyle.Equals는 리플렉션 기반(박싱)이라 직접 비교
    private static bool SameLook(in BoxStyle a, in BoxStyle b) =>
        a.Fill == b.Fill && a.Border == b.Border && a.BorderWidth == b.BorderWidth
        && a.CornerRadius == b.CornerRadius && a.SpriteKey == b.SpriteKey;

    private static CoreRect Inset(CoreRect r, float d) =>
        new(r.X + d, r.Y + d, Math.Max(0f, r.Width - d * 2f), Math.Max(0f, r.Height - d * 2f));

    // 둥근 사각형 둘레의 점. i = 0 ~ 4 * (CornerSegments + 1) - 1, 시계 방향 (화면 기준)
    private static Vector2 Outline(CoreRect r, float radius, int i)
    {
        int corner = i / (CornerSegments + 1);
        int step = i % (CornerSegments + 1);
        float angle = (corner * 90f + 180f + step * 90f / CornerSegments) * Mathf.Deg2Rad; // 왼쪽 위부터
        float cx = corner is 0 or 3 ? r.X + radius : r.X + r.Width - radius;
        float cy = corner is 0 or 1 ? r.Y + radius : r.Y + r.Height - radius;
        return new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius);
    }

    private void RoundedFill(CoreRect r, float radius, Color32 color)
    {
        if (color.a == 0 || r.Width <= 0f || r.Height <= 0f)
            return;

        if (radius <= 0f)
        {
            _mesh.Quad(r.X, r.Y, r.Width, r.Height, color);
            return;
        }

        const int count = 4 * (CornerSegments + 1);
        int center = _mesh.Add(r.X + r.Width / 2f, r.Y + r.Height / 2f, color);
        int first = -1, previous = -1;
        for (int i = 0; i < count; i++)
        {
            Vector2 p = Outline(r, radius, i);
            int index = _mesh.Add(p.x, p.y, color);
            if (previous >= 0)
                _mesh.Triangle(center, previous, index);
            else
                first = index;
            previous = index;
        }

        _mesh.Triangle(center, previous, first);
    }

    private void RoundedRing(CoreRect outer, float radius, float width, Color32 color)
    {
        CoreRect inner = Inset(outer, width);
        float innerRadius = Math.Max(0f, radius - width);
        const int count = 4 * (CornerSegments + 1);
        int start = _mesh.Positions.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 o = Outline(outer, radius, i);
            Vector2 n = Outline(inner, innerRadius, i);
            _mesh.Add(o.x, o.y, color);
            _mesh.Add(n.x, n.y, color);
        }

        for (int i = 0; i < count; i++)
        {
            int o0 = start + i * 2, n0 = o0 + 1;
            int o1 = start + (i + 1) % count * 2, n1 = o1 + 1;
            _mesh.Triangle(o0, o1, n1);
            _mesh.Triangle(o0, n1, n0);
        }
    }
}
