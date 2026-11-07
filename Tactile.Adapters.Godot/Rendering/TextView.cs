using Godot;
using Tactile.Adapters.Godot.Utils;
using CoreColor = Tactile.Core.Primitives.Color;
using CoreRect = Tactile.Core.Primitives.Rect;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>가운데 정렬된 글자 한 줄. 바뀐 값만 노드에 다시 설정합니다.</summary>
internal sealed class TextView : IDisposable
{
    private readonly Label _label = new()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        ClipText = true,
    };

    private bool _hasLast;
    private CoreRect _lastBounds;
    private string? _lastText;
    private CoreColor _lastColor;
    private int _lastFontSize;

    public TextView(Control parent)
    {
        parent.AddChild(_label);
    }

    public void Apply(CoreRect bounds, string text, CoreColor color, float fontSize)
    {
        bool visible = text.Length > 0;
        if (_label.Visible != visible)
            _label.Visible = visible;
        if (!visible)
            return;

        if (!_hasLast || _lastBounds != bounds)
        {
            _label.Position = new Vector2(bounds.X, bounds.Y);
            _label.Size = new Vector2(bounds.Width, bounds.Height);
            _lastBounds = bounds;
        }

        // Core는 값이 바뀔 때만 새 문자열을 만들므로 참조 비교로 충분
        if (!ReferenceEquals(_lastText, text))
        {
            _label.Text = text;
            _lastText = text;
        }

        if (!_hasLast || _lastColor != color)
        {
            _label.AddThemeColorOverride("font_color", color.ToGodot());
            _lastColor = color;
        }

        int size = Math.Max(1, (int)Math.Round(fontSize));
        if (!_hasLast || _lastFontSize != size)
        {
            _label.AddThemeFontSizeOverride("font_size", size);
            _lastFontSize = size;
        }

        _hasLast = true;
    }

    public void Hide()
    {
        if (_label.Visible)
            _label.Visible = false;
    }

    public void Dispose()
    {
        if (GodotObject.IsInstanceValid(_label))
            _label.QueueFree();
    }
}
