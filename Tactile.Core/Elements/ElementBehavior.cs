using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout.Elements;

namespace Tactile.Core.Elements;

/// <summary>
/// 화면 요소 하나의 실행 중 로직. 입력을 받고, 매 프레임 렌더러에 넘길 값을 계산합니다.
/// <para>
/// 요소 종류마다 하나씩 만들어 <see cref="ElementRegistry"/>에 등록합니다. 기본 요소(키, 통계)도 같은 방식으로 등록되어 있습니다.
/// 엔진과 무관한 코드만 두세요. 그리기는 엔진 쪽 렌더러(<see cref="Rendering.IElementRenderer{TVisual}"/>)가 담당합니다.
/// </para>
/// <para>
/// 모든 메서드는 <see cref="KeyViewer"/>를 사용하는 스레드(보통 엔진 메인 스레드)에서 호출됩니다.
/// </para>
/// </summary>
public abstract class ElementBehavior
{
    private protected ElementBehavior(LayoutElement element)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
    }

    /// <summary>이 로직이 담당하는 레이아웃 요소.</summary>
    public LayoutElement Element { get; }

    /// <summary>
    /// 이 요소에 연결된 입력. 여기 들어 있는 입력을 누른 횟수가 통계(KPS, 총 입력 수)에 집계됩니다.
    /// 생성 후에는 바뀌지 않아야 합니다.
    /// </summary>
    public virtual IReadOnlyList<InputBinding> Bindings => Array.Empty<InputBinding>();

    /// <summary>
    /// 입력 이벤트 하나를 받습니다. 모든 요소에 모든 입력이 전달되므로, 필요한 입력만 골라 쓰세요.
    /// 같은 키가 연달아 눌리거나(유실된 뗌) 눌린 적 없는 키가 떼어지는 이벤트는 미리 걸러져 있습니다.
    /// </summary>
    public virtual void OnInput(in KeyboardEvent e)
    {
    }

    /// <summary>매 프레임 입력을 모두 전달한 뒤 호출됩니다. 시간 흐름을 반영하고 렌더러에 넘길 값을 갱신하세요.</summary>
    public abstract void Update(in FrameContext frame);

    /// <summary>입력 이벤트가 유실되어 눌림 상태가 어긋났을 때, 모든 입력을 뗀 상태로 만듭니다.</summary>
    public virtual void ReleaseAll(long now)
    {
    }

    /// <summary>누른 횟수 등 쌓인 상태를 처음으로 되돌립니다.</summary>
    public virtual void Reset()
    {
    }
}

/// <summary>렌더러에 넘길 값(<typeparamref name="TVisual"/>)을 가진 요소 로직.</summary>
public interface IVisualSource<out TVisual>
{
    /// <summary>렌더러에 넘길 값. 매 프레임 같은 인스턴스를 재사용해도 됩니다.</summary>
    TVisual Visual { get; }
}

/// <summary>
/// 요소 로직의 기본 클래스. 요소 종류(<typeparamref name="TElement"/>)와 렌더러에 넘길 값(<typeparamref name="TVisual"/>)을 정합니다.
/// <code>
/// public sealed class GraphBehavior(GraphElement element) : ElementBehavior&lt;GraphElement, GraphVisual&gt;(element)
/// {
///     public override GraphVisual Visual { get; } = new();
///     public override void Update(in FrameContext frame) => Visual.Kps = frame.Statistics.Kps;
/// }
/// </code>
/// </summary>
public abstract class ElementBehavior<TElement, TVisual> : ElementBehavior, IVisualSource<TVisual>
    where TElement : LayoutElement
{
    protected ElementBehavior(TElement element)
        : base(element)
    {
        Element = element;
    }

    /// <summary>이 로직이 담당하는 레이아웃 요소.</summary>
    public new TElement Element { get; }

    public abstract TVisual Visual { get; }
}
