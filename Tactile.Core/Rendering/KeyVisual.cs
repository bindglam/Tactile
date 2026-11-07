using System.Diagnostics;
using System.Globalization;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Primitives;
using Tactile.Core.State;

namespace Tactile.Core.Rendering;

/// <summary>레인 막대 하나.</summary>
/// <param name="Bounds">캔버스 좌표의 사각형</param>
/// <param name="Slot">막대가 사라질 때까지 바뀌지 않는 번호. 렌더러가 노드를 재사용할 때 키로 씁니다.</param>
public readonly record struct RainBar(Rect Bounds, int Slot);

/// <summary>
/// 키 요소를 그리는 데 필요한 값. Core가 매 프레임 계산하고, 같은 인스턴스를 재사용합니다.
/// 글자는 값이 바뀔 때만 새 문자열이 만들어지므로 참조 비교로 바뀌었는지 확인할 수 있습니다.
/// </summary>
public sealed class KeyVisual
{
    /// <summary>아주 짧은 입력도 보이도록 하는 레인 막대의 최소 길이(px).</summary>
    private const float MinRainLength = 2f;

    // 라벨과 누른 횟수를 함께 표시할 때의 위/아래 배치 비율
    private const float LabelHeightRatio = 0.6f;
    private const float CountFontRatio = 0.65f;

    private readonly KeyStyle _style;
    private readonly RainSettings _rainSettings;
    private readonly bool _hasRain;
    private readonly RainBar[] _rain;

    internal KeyVisual(KeyElement element, KeyStyle style, RainSettings rain, bool hasRain, int rainCapacity)
    {
        Element = element;
        _style = style;
        _rainSettings = rain;
        _hasRain = hasRain;
        _rain = new RainBar[rainCapacity];
        Label = element.Label ?? "";
        RainColor = rain.Color;
        Bounds = element.Bounds;
        ShowCount = element.ShowCount;
        Look = style.Normal;
    }

    public KeyElement Element { get; }

    /// <summary>키 박스.</summary>
    public Rect Bounds { get; }

    /// <summary>현재 모양. 누름·뗌 전환 중에는 두 상태 사이 값입니다.</summary>
    public BoxStyle Look { get; private set; }

    /// <summary>0(뗌) ~ 1(누름).</summary>
    public float PressAmount { get; private set; }

    public bool IsPressed { get; private set; }

    public long PressCount { get; private set; }

    public bool HasLabel => Label.Length > 0;

    public string Label { get; }

    public Rect LabelBounds { get; private set; }

    public float LabelFontSize { get; private set; }

    public bool ShowCount { get; }

    /// <summary>누른 횟수 글자. <see cref="ShowCount"/>가 false면 빈 문자열.</summary>
    public string CountText { get; private set; } = "";

    public Rect CountBounds { get; private set; }

    public float CountFontSize { get; private set; }

    public Color RainColor { get; }

    /// <summary>이번 프레임에 그릴 레인 막대 수.</summary>
    public int RainCount { get; private set; }

    /// <summary>레인 막대. 0 ~ <see cref="RainCount"/>-1, 오래된 것부터.</summary>
    public RainBar GetRain(int index)
    {
        if ((uint)index >= (uint)RainCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _rain[index];
    }

    internal void Update(KeyState state, long now)
    {
        KeyStyle style = _style;
        PressAmount = state.PressAmount;
        IsPressed = state.IsPressed;
        Look = PressAmount switch
        {
            <= 0f => style.Normal,
            >= 1f => style.Pressed,
            _ => BoxStyle.Lerp(style.Normal, style.Pressed, PressAmount),
        };

        UpdateText(state);
        UpdateRain(state, now);
    }

    private void UpdateText(KeyState state)
    {
        Rect box = Bounds;
        float fontSize = Look.FontSize;

        if (HasLabel && ShowCount)
        {
            float labelHeight = box.Height * LabelHeightRatio;
            LabelBounds = new Rect(box.X, box.Y, box.Width, labelHeight);
            CountBounds = new Rect(box.X, box.Y + labelHeight, box.Width, box.Height - labelHeight);
            LabelFontSize = fontSize;
            CountFontSize = fontSize * CountFontRatio;
        }
        else
        {
            LabelBounds = box;
            CountBounds = box;
            LabelFontSize = fontSize;
            CountFontSize = fontSize;
        }

        if (ShowCount && (state.PressCount != PressCount || CountText.Length == 0))
            CountText = state.PressCount.ToString(CultureInfo.InvariantCulture);
        PressCount = state.PressCount;
    }

    private void UpdateRain(KeyState state, long now)
    {
        int count = 0;
        if (_hasRain)
        {
            RainSettings rain = _rainSettings;
            Rect box = Bounds;
            double pixelsPerTick = rain.Speed / Stopwatch.Frequency;

            for (int order = 0; order < state.Rain.Count && count < _rain.Length; order++)
            {
                RainSegment segment = state.Rain.Get(order, out int slot);

                // 키 박스 가장자리로부터의 거리: head = 막대의 먼 끝(누른 시점), tail = 가까운 끝(뗀 시점)
                float head = (float)Math.Min(Math.Max(0, now - segment.StartAt) * pixelsPerTick, rain.MaxLength);
                float tail = segment.IsOpen
                    ? 0f
                    : (float)Math.Min(Math.Max(0, now - segment.EndAt) * pixelsPerTick, rain.MaxLength);

                if (tail >= rain.MaxLength)
                    continue;
                if (head - tail < MinRainLength)
                    head = Math.Min(tail + MinRainLength, rain.MaxLength);

                float length = head - tail;
                Rect bounds = rain.Direction switch
                {
                    RainDirection.Up => new Rect(box.X, box.Y - head, box.Width, length),
                    RainDirection.Down => new Rect(box.X, box.Bottom + tail, box.Width, length),
                    RainDirection.Left => new Rect(box.X - head, box.Y, length, box.Height),
                    _ => new Rect(box.Right + tail, box.Y, length, box.Height),
                };

                _rain[count++] = new RainBar(bounds, slot);
            }
        }

        RainCount = count;
    }
}
