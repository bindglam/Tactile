using Tactile.Adapters.Unity.Rendering;
using Tactile.Core;
using Tactile.Core.Elements;
using Tactile.Core.Rendering;
using UnityEngine;

namespace Tactile.Adapters.Unity;

/// <summary><see cref="TactileUnityViewer"/> 설정. 모두 생략할 수 있습니다.</summary>
public sealed class TactileUnityOptions
{
    /// <summary>요소 종류 등록표. null이면 기본 요소(키, 통계, 손)만 등록된 새 등록표를 사용합니다.</summary>
    public ElementRegistry? Elements { get; set; }

    /// <summary>렌더러 등록표. null이면 <see cref="UnityRenderers.CreateDefault"/>로 만든 새 등록표를 사용합니다.</summary>
    public RendererRegistry<UnityRenderContext>? Renderers { get; set; }

    /// <summary>이미지 키(<c>BoxStyle.SpriteKey</c>)를 텍스처로 바꾸는 함수.</summary>
    public Func<string, Texture?>? TextureResolver { get; set; }

    /// <summary>글자 폰트. null이면 Unity 내장 폰트를 사용합니다.</summary>
    public Font? Font { get; set; }

    /// <summary>키 하나에 동시에 표시할 수 있는 최대 레인 막대 수.</summary>
    public int RainCapacity { get; set; } = KeyViewer.DefaultRainCapacity;

    /// <summary>화면 위에 그리는 순서. 클수록 위에 그려집니다.</summary>
    public int SortingOrder { get; set; } = 30000;

    /// <summary>3D 손 모델과 전용 카메라가 쓰는 레이어 (0 ~ 31). 게임이 쓰지 않는 레이어를 고르세요.</summary>
    public int HandsLayer { get; set; } = 31;

    /// <summary>장면이 바뀌어도 유지할지 (<see cref="UnityEngine.Object.DontDestroyOnLoad"/>). 모드에서는 보통 true.</summary>
    public bool KeepAcrossScenes { get; set; } = true;
}
