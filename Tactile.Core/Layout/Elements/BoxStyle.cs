using Tactile.Core.Primitives;

namespace Tactile.Core.Layout;

/// <summary>요소의 한 상태의 모양. 여러 요소가 공용 부품으로 사용합니다.</summary>
public struct BoxStyle
{
    public Color Fill { get; set; }
    public Color Border { get; set; }
    public Color TextColor { get; set; }
    public float BorderWidth { get; set; }
    public float CornerRadius { get; set; }
    public float FontSize { get; set; }

    /// <summary>
    /// 이미지로 그릴 때의 키. 엔진 연결 코드가 실제 텍스처와 연결합니다. null이면 단색 사각형으로 그립니다.
    /// </summary>
    public string? SpriteKey { get; set; }

    /// <summary>두 상태를 t(0~1) 비율로 섞습니다. 이미지 키는 중간(0.5)을 기준으로 바뀝니다.</summary>
    public static BoxStyle Lerp(in BoxStyle from, in BoxStyle to, float t) => new()
    {
        Fill = Color.Lerp(from.Fill, to.Fill, t),
        Border = Color.Lerp(from.Border, to.Border, t),
        TextColor = Color.Lerp(from.TextColor, to.TextColor, t),
        BorderWidth = from.BorderWidth + (to.BorderWidth - from.BorderWidth) * t,
        CornerRadius = from.CornerRadius + (to.CornerRadius - from.CornerRadius) * t,
        FontSize = from.FontSize + (to.FontSize - from.FontSize) * t,
        SpriteKey = t < 0.5f ? from.SpriteKey : to.SpriteKey,
    };
}
