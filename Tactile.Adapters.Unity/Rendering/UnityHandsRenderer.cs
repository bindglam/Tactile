using Tactile.Adapters.Unity.Utils;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using UnityEngine;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>
/// 손 요소를 원작(ADOFAI Macro 3 손 캠)처럼 2D로 그립니다: Core가 투영·정렬한 굵은 선(<see cref="HandsVisual.GetStroke"/>)을
/// 먼 것부터 양 끝이 둥근 선(사각형 + 반원 두 개)의 메시로 만들어 UI에 그립니다. 모양이 바뀐 프레임에만 메시를 다시 만듭니다.
/// 3D 카메라나 렌더 텍스처를 쓰지 않으므로 게임 장면에 아무것도 만들지 않습니다.
/// </summary>
public sealed class UnityHandsRenderer : IElementRenderer<HandsVisual>
{
    private const int CapSegments = 10; // 반원 하나의 조각 수

    private readonly RectTransform _node;
    private readonly BoxView _background;
    private readonly MeshView _strokes;
    private int _drawnRevision = -1;

    public UnityHandsRenderer(HandsElement element, UnityRenderContext context)
    {
        _node = UnityRenderContext.CreateNode($"Hands_{element.Id}", context.ElementLayer);
        _background = new BoxView(_node, context.TextureResolver);
        _strokes = new MeshView("Strokes", _node);
    }

    public void Render(HandsVisual visual)
    {
        _background.Apply(visual.Bounds, visual.Background);
        if (visual.Revision == _drawnRevision)
            return;
        _drawnRevision = visual.Revision;

        _strokes.Clear();
        for (int i = 0; i < visual.StrokeCount; i++)
            AddStroke(visual.GetStroke(i));
        _strokes.Commit();
    }

    /// <summary>양 끝이 둥근 선: 몸통 사각형 + 양 끝 반원 (레이아웃 좌표, y 아래로 증가).</summary>
    private void AddStroke(in HandStroke stroke)
    {
        Color32 color = stroke.Color.ToUnity32();
        float ax = stroke.From.X, ay = stroke.From.Y, bx = stroke.To.X, by = stroke.To.Y;
        float r = stroke.Width / 2f;
        float dx = bx - ax, dy = by - ay;
        float length = Mathf.Sqrt(dx * dx + dy * dy);

        // 선 방향 단위벡터와 그 수직
        float ux = length > 1e-4f ? dx / length : 0f, uy = length > 1e-4f ? dy / length : 1f;
        float nx = -uy * r, ny = ux * r;

        int p0 = _strokes.Add(ax + nx, ay + ny, color);
        int p1 = _strokes.Add(bx + nx, by + ny, color);
        int p2 = _strokes.Add(bx - nx, by - ny, color);
        int p3 = _strokes.Add(ax - nx, ay - ny, color);
        _strokes.Triangle(p0, p1, p2);
        _strokes.Triangle(p0, p2, p3);

        // 시작점 쪽 반원 (선 반대 방향), 끝점 쪽 반원 (선 방향)
        float startAngle = Mathf.Atan2(ny, nx);
        Cap(ax, ay, r, startAngle, color);
        Cap(bx, by, r, startAngle + Mathf.PI, color);
    }

    private void Cap(float cx, float cy, float r, float startAngle, Color32 color)
    {
        int center = _strokes.Add(cx, cy, color);
        int previous = -1;
        for (int i = 0; i <= CapSegments; i++)
        {
            float a = startAngle + Mathf.PI * i / CapSegments;
            int index = _strokes.Add(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, color);
            if (previous >= 0)
                _strokes.Triangle(center, previous, index);
            previous = index;
        }
    }

    public void Dispose()
    {
        _background.Dispose();
        _strokes.Dispose();
        if (_node != null)
            UnityEngine.Object.Destroy(_node.gameObject);
    }
}
