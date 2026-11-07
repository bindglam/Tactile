using Godot;
using Tactile.Adapters.Godot.Rendering;
using Tactile.Core;
using Tactile.Core.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Godot;

/// <summary><see cref="TactileGodotViewer"/> 설정. 모두 생략할 수 있습니다.</summary>
public sealed class TactileGodotOptions
{
    /// <summary>요소 종류 등록표. null이면 기본 요소(키, 통계)만 등록된 새 등록표를 사용합니다.</summary>
    public ElementRegistry? Elements { get; set; }

    /// <summary>렌더러 등록표. null이면 <see cref="GodotRenderers.CreateDefault"/>로 만든 새 등록표를 사용합니다.</summary>
    public RendererRegistry<GodotRenderContext>? Renderers { get; set; }

    /// <summary>이미지 키(<c>BoxStyle.SpriteKey</c>)를 텍스처로 바꾸는 함수.</summary>
    public Func<string, Texture2D?>? TextureResolver { get; set; }

    /// <summary>키 하나에 동시에 표시할 수 있는 최대 레인 막대 수.</summary>
    public int RainCapacity { get; set; } = KeyViewer.DefaultRainCapacity;
}
