using Tactile.Adapters.Unity.Utils;
using UnityEngine;
using CoreColor = Tactile.Core.Primitives.Color;
using CoreRect = Tactile.Core.Primitives.Rect;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>가운데 정렬된 글자 한 줄. 글자·크기·색·위치가 바뀌었을 때만 메시를 다시 만듭니다.</summary>
internal sealed class TextView : IDisposable
{
    private readonly MeshView _mesh;
    private readonly Font _font;
    private bool _hasLast;
    private bool _dirty;
    private CoreRect _lastBounds;
    private string? _lastText;
    private CoreColor _lastColor;
    private int _lastFontSize;

    public TextView(Transform parent, Font font)
    {
        _mesh = new MeshView("Text", parent);
        _font = font;
        Font.textureRebuilt += OnFontTextureRebuilt;
    }

    public void Apply(CoreRect bounds, string text, CoreColor color, float fontSize)
    {
        bool visible = text.Length > 0;
        _mesh.SetVisible(visible);
        if (!visible)
            return;

        int size = Math.Max(1, (int)Math.Round(fontSize));

        // Core는 값이 바뀔 때만 새 문자열을 만들므로 참조 비교로 충분
        if (_hasLast && !_dirty && ReferenceEquals(_lastText, text) && _lastBounds == bounds
            && _lastColor == color && _lastFontSize == size)
            return;

        _hasLast = true;
        _dirty = false;
        _lastText = text;
        _lastBounds = bounds;
        _lastColor = color;
        _lastFontSize = size;
        Rebuild();
    }

    public void Hide() => _mesh.SetVisible(false);

    public void Dispose()
    {
        Font.textureRebuilt -= OnFontTextureRebuilt;
        _mesh.Dispose();
    }

    private void OnFontTextureRebuilt(Font font)
    {
        // 폰트 아틀라스가 다시 만들어지면 UV가 바뀌므로 다음 프레임에 다시 만듦
        if (font == _font)
            _dirty = true;
    }

    private void Rebuild()
    {
        // TextGenerator/UIVertex는 Unity 버전마다 필드 형식이 달라(예: uv0) 다른 버전의 게임에서 깨지므로,
        // 오래전부터 형태가 같은 Font.GetCharacterInfo로 글자를 직접 배치함
        string text = _lastText!;
        int size = _lastFontSize;
        _font.RequestCharactersInTexture(text, size, FontStyle.Normal);

        // 1. 전체 너비(advance 합)와 글자 모양의 위·아래 끝
        float width = 0f, minY = float.MaxValue, maxY = float.MinValue;
        foreach (char c in text)
        {
            if (!_font.GetCharacterInfo(c, out CharacterInfo info, size, FontStyle.Normal))
                continue;
            width += info.advance;
            if (info.glyphHeight > 0)
            {
                minY = Math.Min(minY, info.minY);
                maxY = Math.Max(maxY, info.maxY);
            }
        }

        if (minY > maxY)
            minY = maxY = 0f;

        // 2. 영역 가운데에 글자 모양 기준으로 정렬 (y는 글자 좌표에서 위로 증가, 레이아웃 좌표에서 아래로 증가)
        Color32 color = _lastColor.ToUnity32();
        float pen = _lastBounds.X + (_lastBounds.Width - width) / 2f;
        float baseline = _lastBounds.Y + _lastBounds.Height / 2f + (maxY + minY) / 2f;

        _mesh.Clear();
        _mesh.SetTexture(_font.material.mainTexture);
        foreach (char c in text)
        {
            if (!_font.GetCharacterInfo(c, out CharacterInfo info, size, FontStyle.Normal))
                continue;

            if (info.glyphWidth > 0 && info.glyphHeight > 0)
            {
                float left = pen + info.minX, right = pen + info.maxX;
                float top = baseline - info.maxY, bottom = baseline - info.minY;
                int a = _mesh.Add(left, top, color, info.uvTopLeft);
                int b = _mesh.Add(right, top, color, info.uvTopRight);
                int d = _mesh.Add(right, bottom, color, info.uvBottomRight);
                int e = _mesh.Add(left, bottom, color, info.uvBottomLeft);
                _mesh.Triangle(a, b, d);
                _mesh.Triangle(a, d, e);
            }

            pen += info.advance;
        }

        _mesh.Commit();
    }
}
