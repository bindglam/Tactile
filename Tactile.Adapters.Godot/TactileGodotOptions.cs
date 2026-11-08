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

    /// <summary>
    /// 이미지 키(<c>BoxStyle.SpriteKey</c>)를 텍스처로 바꾸는 함수. null을 돌려주거나 지정하지 않으면
    /// 레이아웃 리소스(<c>KeyViewerLayout.Assets</c>)에서 같은 이름의 이미지를 찾습니다.
    /// </summary>
    public Func<string, Texture2D?>? TextureResolver { get; set; }

    /// <summary>레이아웃 리소스의 텍스처 캐시. null이면 키뷰어마다 새로 만듭니다. 여러 키뷰어가 공유하면 다시 디코딩하지 않습니다.</summary>
    public GodotAssetTextures? AssetTextures { get; set; }

    /// <summary>키 하나에 동시에 표시할 수 있는 최대 레인 막대 수.</summary>
    public int RainCapacity { get; set; } = KeyViewer.DefaultRainCapacity;
}
