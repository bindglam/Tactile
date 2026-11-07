using Godot;
using Tactile.Adapters.Godot.Utils;
using Tactile.Core.Layout;
using CoreRect = Tactile.Core.Primitives.Rect;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>
/// 요소의 배경 상자. <see cref="BoxStyle.SpriteKey"/>가 있으면 이미지, 없으면 둥근 사각형으로 그립니다.
/// 바뀐 값만 노드에 다시 설정합니다.
/// </summary>
internal sealed class BoxView : IDisposable
{
    private readonly Panel _panel = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly StyleBoxFlat _style = new();
    private readonly TextureRect _sprite = new()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        Visible = false,
    };

    private readonly Func<string, Texture2D?>? _textureResolver;
    private bool _hasLast;
    private CoreRect _lastBounds;
    private BoxStyle _last;

    public BoxView(Control parent, Func<string, Texture2D?>? textureResolver)
    {
        _textureResolver = textureResolver;
        _panel.AddThemeStyleboxOverride("panel", _style);
        parent.AddChild(_panel);
        parent.AddChild(_sprite);
    }

    public void Apply(CoreRect bounds, in BoxStyle look)
    {
        if (!_hasLast || _lastBounds != bounds)
        {
            var position = new Vector2(bounds.X, bounds.Y);
            var size = new Vector2(bounds.Width, bounds.Height);
            _panel.Position = _sprite.Position = position;
            _panel.Size = _sprite.Size = size;
        }

        bool useSprite = look.SpriteKey is not null;
        if (!_hasLast || (_last.SpriteKey is not null) != useSprite)
        {
            _panel.Visible = !useSprite;
            _sprite.Visible = useSprite;
        }

        if (useSprite)
        {
            if (!_hasLast || _last.SpriteKey != look.SpriteKey)
                _sprite.Texture = _textureResolver?.Invoke(look.SpriteKey!);
            if (!_hasLast || _last.Fill != look.Fill)
                _sprite.Modulate = look.Fill.ToGodot();
        }
        else
        {
            if (!_hasLast || _last.Fill != look.Fill)
                _style.BgColor = look.Fill.ToGodot();
            if (!_hasLast || _last.Border != look.Border)
                _style.BorderColor = look.Border.ToGodot();
            if (!_hasLast || _last.BorderWidth != look.BorderWidth)
                _style.SetBorderWidthAll((int)Math.Round(look.BorderWidth));
            if (!_hasLast || _last.CornerRadius != look.CornerRadius)
                _style.SetCornerRadiusAll((int)Math.Round(look.CornerRadius));
        }

        _lastBounds = bounds;
        _last = look;
        _hasLast = true;
    }

    public void Dispose()
    {
        if (GodotObject.IsInstanceValid(_panel))
            _panel.QueueFree();
        if (GodotObject.IsInstanceValid(_sprite))
            _sprite.QueueFree();
    }
}
