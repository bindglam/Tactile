using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>Unity 렌더러 등록표.</summary>
public static class UnityRenderers
{
    /// <summary>
    /// 기본 요소(키, 통계, 손)의 렌더러가 등록된 등록표를 만듭니다.
    /// 애드온 요소의 렌더러는 여기에 추가로 등록하세요.
    /// </summary>
    public static RendererRegistry<UnityRenderContext> CreateDefault()
    {
        var registry = new RendererRegistry<UnityRenderContext>();
        registry.Register<KeyElement, KeyVisual>((element, context) => new UnityKeyRenderer(element, context));
        registry.Register<StatElement, StatVisual>((element, context) => new UnityStatRenderer(element, context));
        registry.Register<HandsElement, HandsVisual>((element, context) => new UnityHandsRenderer(element, context));
        return registry;
    }
}
