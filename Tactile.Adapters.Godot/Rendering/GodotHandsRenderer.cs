using Godot;
using Tactile.Adapters.Godot.Utils;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>
/// 손 요소를 원작(ADOFAI Macro 3 손 캠)처럼 2D로 그립니다: Core가 투영·정렬한 굵은 선(<see cref="HandsVisual.GetStroke"/>)을
/// 먼 것부터 양 끝이 둥근 선으로 그립니다. 모양이 바뀐 프레임에만 다시 그립니다.
/// </summary>
public sealed class GodotHandsRenderer : IElementRenderer<HandsVisual>
{
    private readonly Control _node;
    private readonly BoxView _background;
    private readonly Control _canvas;
    private HandsVisual? _visual;
    private int _drawnRevision = -1;

    public GodotHandsRenderer(HandsElement element, GodotRenderContext context)
    {
        _node = GodotRenderContext.CreateContainer($"Hands_{element.Id}");
        context.ElementLayer.AddChild(_node);
        _background = new BoxView(_node, context.TextureResolver);

        // 라이브러리 클래스는 _Draw를 override할 수 없으므로 Draw 시그널 사용
        _canvas = GodotRenderContext.CreateContainer("Strokes");
        _canvas.Draw += OnDraw;
        _node.AddChild(_canvas);
    }

    public void Render(HandsVisual visual)
    {
        _background.Apply(visual.Bounds, visual.Background);
        _visual = visual;
        if (visual.Revision != _drawnRevision)
        {
            _drawnRevision = visual.Revision;
            _canvas.QueueRedraw();
        }
    }

    private void OnDraw()
    {
        if (_visual is null)
            return;

        for (int i = 0; i < _visual.StrokeCount; i++)
        {
            HandStroke stroke = _visual.GetStroke(i);
            var color = stroke.Color.ToGodot();
            var from = new Vector2(stroke.From.X, stroke.From.Y);
            var to = new Vector2(stroke.To.X, stroke.To.Y);
            float radius = stroke.Width / 2f;

            _canvas.DrawLine(from, to, color, stroke.Width, antialiased: true);
            _canvas.DrawCircle(from, radius, color, filled: true, width: -1f, antialiased: true);
            _canvas.DrawCircle(to, radius, color, filled: true, width: -1f, antialiased: true);
        }
    }

    public void Dispose()
    {
        _background.Dispose();
        if (GodotObject.IsInstanceValid(_canvas))
            _canvas.Draw -= OnDraw;
        if (GodotObject.IsInstanceValid(_node))
            _node.QueueFree();
    }
}
