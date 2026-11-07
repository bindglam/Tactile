using Tactile.Adapters.Unity.Utils;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using UnityEngine;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>키 요소 하나를 그립니다: 배경 상자, 라벨, 누른 횟수, 레인 막대.</summary>
public sealed class UnityKeyRenderer : IElementRenderer<KeyVisual>
{
    private readonly RectTransform _node;
    private readonly BoxView _box;
    private readonly TextView _label;
    private readonly TextView _count;
    private readonly MeshView _rain;
    private bool _rainEmpty = true;

    public UnityKeyRenderer(KeyElement element, UnityRenderContext context)
    {
        _node = UnityRenderContext.CreateNode($"Key_{element.Id}", context.ElementLayer);
        _box = new BoxView(_node, context.TextureResolver);
        _label = new TextView(_node, context.Font);
        _count = new TextView(_node, context.Font);
        _rain = new MeshView($"Rain_{element.Id}", context.RainLayer);
    }

    public void Render(KeyVisual visual)
    {
        _box.Apply(visual.Bounds, visual.Look);

        if (visual.HasLabel)
            _label.Apply(visual.LabelBounds, visual.Label, visual.Look.TextColor, visual.LabelFontSize);
        else
            _label.Hide();

        if (visual.ShowCount)
            _count.Apply(visual.CountBounds, visual.CountText, visual.Look.TextColor, visual.CountFontSize);
        else
            _count.Hide();

        RenderRain(visual);
    }

    private void RenderRain(KeyVisual visual)
    {
        // 막대가 없던 상태가 이어지면 메시를 다시 만들지 않음
        if (visual.RainCount == 0 && _rainEmpty)
            return;

        _rain.Clear();
        Color32 color = visual.RainColor.ToUnity32();
        for (int i = 0; i < visual.RainCount; i++)
        {
            var b = visual.GetRain(i).Bounds;
            _rain.Quad(b.X, b.Y, b.Width, b.Height, color);
        }

        _rain.Commit();
        _rainEmpty = visual.RainCount == 0;
    }

    public void Dispose()
    {
        _box.Dispose();
        _label.Dispose();
        _count.Dispose();
        _rain.Dispose();
        if (_node != null)
            UnityEngine.Object.Destroy(_node.gameObject);
    }
}
