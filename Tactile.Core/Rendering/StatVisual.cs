using System.Globalization;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Primitives;
using Tactile.Core.State;

namespace Tactile.Core.Rendering;

/// <summary>
/// 통계 요소를 그리는 데 필요한 값. Core가 매 프레임 계산하고, 같은 인스턴스를 재사용합니다.
/// <see cref="Text"/>는 표시 값이 바뀔 때만 새 문자열이 만들어집니다.
/// </summary>
public sealed class StatVisual
{
    private long _textKey = long.MinValue;

    internal StatVisual(StatElement element)
    {
        Element = element;
        Bounds = element.Bounds;
        Look = element.Look;
    }

    public StatElement Element { get; }

    public Rect Bounds { get; }

    public BoxStyle Look { get; }

    /// <summary>통계 값 (KPS, 최대 KPS, 총 입력 수).</summary>
    public double Value { get; private set; }

    /// <summary><see cref="StatElement.Format"/>을 적용한 글자.</summary>
    public string Text { get; private set; } = "";

    internal void Update(InputStatistics statistics)
    {
        Value = Element.Kind switch
        {
            StatKind.Kps => statistics.Kps,
            StatKind.MaxKps => statistics.MaxKps,
            _ => statistics.Total,
        };

        // 표시 정밀도(소수 둘째 자리)까지 같으면 문자열을 다시 만들지 않음
        long key = (long)Math.Round(Value * 100);
        if (key != _textKey)
        {
            _textKey = key;
            Text = Format(Element, Value);
        }
    }

    private static string Format(StatElement element, double value)
    {
        object boxed = element.Kind == StatKind.Total ? (long)value : value;
        try
        {
            return string.Format(CultureInfo.InvariantCulture, element.Format ?? "{0}", boxed);
        }
        catch (FormatException)
        {
            return Convert.ToString(boxed, CultureInfo.InvariantCulture) ?? "";
        }
    }
}
