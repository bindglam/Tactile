using Godot;
using Tactile.Adapters.Godot.Utils;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>키 요소 하나를 그립니다: 배경 상자, 라벨, 누른 횟수, 레인 막대.</summary>
public sealed class GodotKeyRenderer : IElementRenderer<KeyVisual>
{
    private readonly Control _node;
    private readonly Control _rainNode;
    private readonly BoxView _box;
    private readonly TextView _label;
    private readonly TextView _count;

    // 레인 막대 노드. 슬롯 번호로 재사용하고, 이번 프레임에 없는 것은 숨김
    private readonly Dictionary<int, ColorRect> _rainBars = new();
    private readonly HashSet<int> _visibleSlots = new();
    private readonly List<int> _hideBuffer = new();

    public GodotKeyRenderer(KeyElement element, GodotRenderContext context)
    {
        _node = GodotRenderContext.CreateContainer($"Key_{element.Id}");
        _rainNode = GodotRenderContext.CreateContainer($"Rain_{element.Id}");
        context.ElementLayer.AddChild(_node);
        context.RainLayer.AddChild(_rainNode);

        _box = new BoxView(_node, context.TextureResolver);
        _label = new TextView(_node);
        _count = new TextView(_node);
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
        Color color = visual.RainColor.ToGodot();

        // 이번 프레임의 막대 표시
        _hideBuffer.Clear();
        foreach (int slot in _visibleSlots)
            _hideBuffer.Add(slot);

        for (int i = 0; i < visual.RainCount; i++)
        {
            RainBar bar = visual.GetRain(i);
            if (!_rainBars.TryGetValue(bar.Slot, out ColorRect? rect))
            {
                rect = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Color = color };
                _rainNode.AddChild(rect);
                _rainBars[bar.Slot] = rect;
            }

            rect.Position = new Vector2(bar.Bounds.X, bar.Bounds.Y);
            rect.Size = new Vector2(bar.Bounds.Width, bar.Bounds.Height);
            if (!rect.Visible)
                rect.Visible = true;

            _visibleSlots.Add(bar.Slot);
            _hideBuffer.Remove(bar.Slot);
        }

        // 사라진 막대는 숨김 (노드는 다시 쓰일 수 있으므로 남겨 둠)
        foreach (int slot in _hideBuffer)
        {
            _rainBars[slot].Visible = false;
            _visibleSlots.Remove(slot);
        }
    }

    public void Dispose()
    {
        _box.Dispose();
        _label.Dispose();
        _count.Dispose();
        if (GodotObject.IsInstanceValid(_node))
            _node.QueueFree();
        if (GodotObject.IsInstanceValid(_rainNode))
            _rainNode.QueueFree();
        _rainBars.Clear();
    }
}
