using Tactile.Core.Primitives;

namespace Tactile.Core.Layout.Elements;

/// <summary>키 하나(또는 여러 키를 묶은 칸)를 나타내는 요소.</summary>
public sealed class KeyElement : LayoutElement
{
    /// <summary>이 칸에 연결된 입력. 여러 개면 그중 하나라도 눌려 있으면 눌린 것으로 봅니다.</summary>
    public List<InputBinding> Bindings { get; set; } = new();

    /// <summary>칸에 표시할 글자.</summary>
    public string Label { get; set; } = "";

    /// <summary>모양 (평소, 눌림, 전환 시간).</summary>
    public KeyStyle Style { get; set; } = new();
    
    /// <summary>이 키만의 레인 설정.</summary>
    public RainSettings Rain { get; set; } = RainSettings.Default;

    /// <summary>누른 횟수를 표시할지.</summary>
    public bool ShowCount { get; set; }
}

/// <summary>
/// 키 요소의 모양. 평소와 눌렸을 때의 모양을 따로 정합니다.
/// 새로 만들면 기본 모양(어두운 칸, 누르면 흰색)으로 채워집니다.
/// </summary>
public sealed class KeyStyle
{
    /// <summary>기본 평소 모양.</summary>
    public static BoxStyle DefaultNormal => new()
    {
        Fill = Color.FromRgba(30, 30, 30, 200),
        Border = Color.FromRgba(255, 255, 255, 120),
        TextColor = Color.White,
        BorderWidth = 2f,
        CornerRadius = 6f,
        FontSize = 18f,
    };

    /// <summary>기본 눌림 모양.</summary>
    public static BoxStyle DefaultPressed => new()
    {
        Fill = Color.FromRgba(255, 255, 255, 230),
        Border = Color.White,
        TextColor = Color.Black,
        BorderWidth = 2f,
        CornerRadius = 6f,
        FontSize = 18f,
    };

    /// <summary>기본 전환 시간(ms).</summary>
    public const float DefaultTransitionMs = 60f;

    public BoxStyle Normal { get; set; } = DefaultNormal;

    public BoxStyle Pressed { get; set; } = DefaultPressed;

    /// <summary>평소 ↔ 눌림 모양이 바뀌는 데 걸리는 시간(ms). 0이면 즉시 바뀝니다.</summary>
    public float TransitionMs { get; set; } = DefaultTransitionMs;

    /// <summary>값을 복사한 새 스타일. 에디터에서 다른 키에 같은 스타일을 적용할 때 사용합니다.</summary>
    public KeyStyle Clone() => new() { Normal = Normal, Pressed = Pressed, TransitionMs = TransitionMs };
}

/// <summary>레인이 흘러가는 방향 (키 박스 기준).</summary>
public enum RainDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// 레인(키를 누르는 동안 막대가 키 박스에서 흘러나가는 표시) 설정.
/// 막대는 키 박스의 가장자리에 붙어서 <see cref="Direction"/> 쪽으로 이동합니다.
/// </summary>
public struct RainSettings
{
    public static RainSettings Default => new()
    {
        Enabled = true,
        Direction = RainDirection.Up,
        Speed = 600f,
        MaxLength = 400f,
        Color = Color.FromRgba(255, 255, 255, 180),
    };

    public bool Enabled { get; set; }

    public RainDirection Direction { get; set; }

    /// <summary>이동 속도 (px/s). 0 이하면 레인을 그리지 않습니다.</summary>
    public float Speed { get; set; }

    /// <summary>키 박스에서 이 거리(px)를 넘어간 부분은 잘리고, 완전히 넘어간 막대는 사라집니다.</summary>
    public float MaxLength { get; set; }

    public Color Color { get; set; }
}
