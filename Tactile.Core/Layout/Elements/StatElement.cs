namespace Tactile.Core.Layout.Elements;

/// <summary>통계 값을 글자로 표시하는 요소.</summary>
public sealed class StatElement : LayoutElement
{
    public StatKind Kind { get; set; }

    /// <summary>모양. 통계는 눌림 상태가 없으므로 하나만 가집니다.</summary>
    public BoxStyle Look { get; set; } = KeyStyle.DefaultNormal;

    /// <summary><see cref="string.Format(string, object)"/> 형식. 예: <c>"KPS {0:0}"</c></summary>
    public string Format { get; set; } = "{0}";
}

/// <summary>표시할 통계 종류.</summary>
public enum StatKind
{
    /// <summary>최근 1초 동안 누른 횟수.</summary>
    Kps,

    /// <summary>지금까지의 최대 KPS.</summary>
    MaxKps,

    /// <summary>레이아웃에 연결된 키를 누른 총 횟수.</summary>
    Total,
}
