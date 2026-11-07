using UnityEngine;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>
/// Unity 렌더러가 받는 정보. 키뷰어 하나마다 만들어집니다.
/// <para>
/// 좌표: <see cref="Root"/>의 로컬 좌표에서 레이아웃 좌표 (x, y)는 Unity의 (x, -y)입니다 (Unity UI는 y가 위로 증가).
/// <see cref="RainLayer"/>가 <see cref="ElementLayer"/>보다 먼저 그려지므로, 레인은 항상 요소 밑에 있습니다.
/// </para>
/// </summary>
public sealed class UnityRenderContext
{
    internal UnityRenderContext(RectTransform root, TactileUnityOptions options)
    {
        Root = root;
        TextureResolver = options.TextureResolver;
        Font = options.Font ?? LoadBuiltinFont();
        HandsLayer = options.HandsLayer;
        KeepAcrossScenes = options.KeepAcrossScenes;

        RainLayer = CreateNode("Rain", root);
        ElementLayer = CreateNode("Elements", root);
    }

    /// <summary>키뷰어 전체의 부모. 로컬 좌표는 레이아웃 픽셀 단위입니다.</summary>
    public RectTransform Root { get; }

    /// <summary>레인처럼 모든 요소보다 아래에 그릴 오브젝트를 넣는 곳.</summary>
    public RectTransform RainLayer { get; }

    /// <summary>요소 오브젝트를 넣는 곳. 레이아웃 순서대로 추가되므로 나중 요소가 위에 그려집니다.</summary>
    public RectTransform ElementLayer { get; }

    /// <summary>이미지 키(<c>BoxStyle.SpriteKey</c>)를 텍스처로 바꾸는 함수.</summary>
    public Func<string, Texture?>? TextureResolver { get; }

    /// <summary>글자에 쓰는 폰트.</summary>
    public Font Font { get; }

    /// <summary>3D 손 모델과 전용 카메라가 쓰는 레이어.</summary>
    public int HandsLayer { get; }

    /// <summary>캔버스 밖에 만드는 오브젝트(3D 손 모델 등)도 장면이 바뀌어도 유지해야 하는지.</summary>
    public bool KeepAcrossScenes { get; }

    /// <summary>부모 기준 원점에 있는 빈 UI 오브젝트를 만듭니다.</summary>
    public static RectTransform CreateNode(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Font LoadBuiltinFont()
    {
        // Unity 2022.2부터 내장 폰트 이름이 바뀜
        foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
        {
            try
            {
                Font? font = Resources.GetBuiltinResource<Font>(name);
                if (font != null)
                    return font;
            }
            catch (ArgumentException)
            {
            }
        }

        return Font.CreateDynamicFontFromOSFont("Arial", 32);
    }
}
