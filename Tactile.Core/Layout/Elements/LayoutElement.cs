using Tactile.Core.Primitives;

namespace Tactile.Core.Layout.Elements;

/// <summary>
/// 화면 요소의 공통 부분. 모양 같은 나머지 속성은 요소 종류마다 직접 가집니다.
/// </summary>
public abstract class LayoutElement
{
    /// <summary>요소 고유 ID. 레이아웃 안에서 겹치면 안 됩니다.</summary>
    public string Id { get; set; } = "";

    /// <summary>위치와 크기.</summary>
    public Rect Bounds { get; set; }
}
