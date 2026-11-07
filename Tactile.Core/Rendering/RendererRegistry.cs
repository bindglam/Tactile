using Tactile.Core.Elements;
using Tactile.Core.Layout.Elements;

namespace Tactile.Core.Rendering;

/// <summary>
/// 요소 종류별 렌더러 등록표. 엔진마다 하나씩 사용합니다.
/// <code>
/// var renderers = GodotRenderers.CreateDefault();
/// renderers.Register&lt;GraphElement, GraphVisual&gt;((element, context) => new GraphRenderer(element, context));
/// </code>
/// <para>
/// <typeparamref name="TContext"/>는 엔진이 렌더러에 넘기는 정보(부모 노드, 텍스처 조회 등)입니다.
/// 등록은 엔진 노드가 만들어지기 전에 할 수 있고, 실제 정보는 <see cref="Bind"/>로 키뷰어를 만들 때 연결됩니다.
/// 요소 클래스에 등록된 렌더러가 없으면 부모 클래스에 등록된 렌더러를 사용합니다.
/// </para>
/// </summary>
public sealed class RendererRegistry<TContext>
{
    private readonly Dictionary<Type, Func<ElementBehavior, TContext, IElementView>> _factories = new();

    /// <summary>요소 종류의 렌더러를 등록합니다. 이미 있으면 바꿉니다 (기본 렌더러를 직접 만든 렌더러로 바꿀 때).</summary>
    /// <typeparam name="TElement">레이아웃 요소 클래스</typeparam>
    /// <typeparam name="TVisual">요소 로직이 넘겨 주는 값의 형식 (<see cref="IVisualSource{TVisual}"/>)</typeparam>
    /// <param name="factory">렌더러를 만드는 함수. 키뷰어마다 요소 하나당 한 번 호출됩니다.</param>
    public void Register<TElement, TVisual>(Func<TElement, TContext, IElementRenderer<TVisual>> factory)
        where TElement : LayoutElement
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        _factories[typeof(TElement)] = (behavior, context) =>
        {
            if (behavior is not IVisualSource<TVisual> source)
            {
                throw new InvalidOperationException(
                    $"'{behavior.Element.Id}' 요소의 로직({behavior.GetType().Name})이 렌더러가 기대하는 값({typeof(TVisual).Name})을 제공하지 않습니다.");
            }

            IElementRenderer<TVisual> renderer = factory((TElement)behavior.Element, context)
                ?? throw new InvalidOperationException($"{typeof(TElement).Name} 렌더러를 만드는 함수가 null을 반환했습니다.");
            return new View<TVisual>(source, renderer);
        };
    }

    /// <summary>요소 종류의 렌더러 등록을 지웁니다.</summary>
    public bool Unregister<TElement>()
        where TElement : LayoutElement =>
        _factories.Remove(typeof(TElement));

    /// <summary>요소 클래스(또는 부모 클래스)에 렌더러가 등록되어 있는지 확인합니다.</summary>
    public bool IsRegistered(Type elementType) => Find(elementType) is not null;

    /// <summary>엔진 정보를 연결해 <see cref="KeyViewer"/>에 넘길 수 있는 형태로 만듭니다.</summary>
    public IElementViewProvider Bind(TContext context) => new Provider(this, context);

    private Func<ElementBehavior, TContext, IElementView>? Find(Type? type)
    {
        for (; type is not null && type != typeof(object); type = type.BaseType)
        {
            if (_factories.TryGetValue(type, out Func<ElementBehavior, TContext, IElementView>? factory))
                return factory;
        }

        return null;
    }

    private sealed class Provider(RendererRegistry<TContext> registry, TContext context) : IElementViewProvider
    {
        public IElementView? CreateView(ElementBehavior behavior) =>
            registry.Find(behavior.Element.GetType())?.Invoke(behavior, context);
    }

    private sealed class View<TVisual>(IVisualSource<TVisual> source, IElementRenderer<TVisual> renderer) : IElementView
    {
        public void Render() => renderer.Render(source.Visual);

        public void Dispose() => renderer.Dispose();
    }
}
