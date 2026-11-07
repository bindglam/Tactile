using Tactile.Core.Elements;

namespace Tactile.Core.Rendering;

/// <summary>
/// 요소 하나를 그리는 렌더러. 엔진 쪽에서 구현해 <see cref="RendererRegistry{TContext}"/>에 등록합니다.
/// <para>
/// 위치·색·글자 같은 수치는 요소 로직(<see cref="ElementBehavior"/>)이 계산해서 <typeparamref name="TVisual"/>로 넘겨 주므로,
/// 렌더러는 받은 값을 그대로 그리기만 하면 됩니다.
/// <typeparamref name="TVisual"/> 객체는 매 프레임 같은 인스턴스가 재사용될 수 있으므로 참조를 보관하지 말고 값만 읽으세요.
/// </para>
/// </summary>
public interface IElementRenderer<in TVisual> : IDisposable
{
    /// <summary>매 프레임 <see cref="KeyViewer.Update()"/> 안에서, 요소 로직이 값을 갱신한 직후 호출됩니다.</summary>
    void Render(TVisual visual);
}

/// <summary>요소 로직과 렌더러를 이어 둔 것. <see cref="KeyViewer"/>가 매 프레임 <see cref="Render"/>를 호출합니다.</summary>
public interface IElementView : IDisposable
{
    void Render();
}

/// <summary>요소 로직에 맞는 렌더러를 만들어 <see cref="IElementView"/>로 돌려줍니다.</summary>
public interface IElementViewProvider
{
    /// <summary>요소를 그릴 렌더러를 만듭니다. 이 요소 종류의 렌더러가 없으면 null (그리지 않음).</summary>
    IElementView? CreateView(ElementBehavior behavior);
}
