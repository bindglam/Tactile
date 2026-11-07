using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using UnityEngine;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>통계 요소 하나를 그립니다: 배경 상자와 글자.</summary>
public sealed class UnityStatRenderer : IElementRenderer<StatVisual>
{
    private readonly RectTransform _node;
    private readonly BoxView _box;
    private readonly TextView _text;

    public UnityStatRenderer(StatElement element, UnityRenderContext context)
    {
        _node = UnityRenderContext.CreateNode($"Stat_{element.Id}", context.ElementLayer);
        _box = new BoxView(_node, context.TextureResolver);
        _text = new TextView(_node, context.Font);
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
        if (_node != null)
            UnityEngine.Object.Destroy(_node.gameObject);
    }
}
