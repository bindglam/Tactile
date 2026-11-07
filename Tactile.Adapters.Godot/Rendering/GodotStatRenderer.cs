using Godot;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>통계 요소 하나를 그립니다: 배경 상자와 글자.</summary>
public sealed class GodotStatRenderer : IElementRenderer<StatVisual>
{
    private readonly Control _node;
    private readonly BoxView _box;
    private readonly TextView _text;

    public GodotStatRenderer(StatElement element, GodotRenderContext context)
    {
        _node = GodotRenderContext.CreateContainer($"Stat_{element.Id}");
        context.ElementLayer.AddChild(_node);

        _box = new BoxView(_node, context.TextureResolver);
        _text = new TextView(_node);
    }

    public void Render(StatVisual visual)
    {
        _box.Apply(visual.Bounds, visual.Look);
        _text.Apply(visual.Bounds, visual.Text, visual.Look.TextColor, visual.Look.FontSize);
    }

    public void Dispose()
    {
        _box.Dispose();
        _text.Dispose();
        if (GodotObject.IsInstanceValid(_node))
            _node.QueueFree();
    }
}
