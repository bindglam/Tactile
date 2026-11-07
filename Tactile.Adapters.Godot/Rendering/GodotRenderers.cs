using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>Godot 렌더러 등록표.</summary>
public static class GodotRenderers
{
    /// <summary>
    /// 기본 요소(키, 통계, 손)의 렌더러가 등록된 등록표를 만듭니다.
    /// 애드온 요소의 렌더러는 여기에 추가로 등록하세요.
    /// </summary>
    public static RendererRegistry<GodotRenderContext> CreateDefault()
    {
        var registry = new RendererRegistry<GodotRenderContext>();
        registry.Register<KeyElement, KeyVisual>((element, context) => new GodotKeyRenderer(element, context));
        registry.Register<StatElement, StatVisual>((element, context) => new GodotStatRenderer(element, context));
        registry.Register<HandsElement, HandsVisual>((element, context) => new GodotHandsRenderer(element, context));
        return registry;
    }
}
