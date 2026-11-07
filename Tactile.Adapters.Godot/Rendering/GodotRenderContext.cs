using Godot;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>
/// Godot 렌더러가 받는 정보. 키뷰어 하나마다 만들어집니다.
/// <see cref="RainLayer"/>가 <see cref="ElementLayer"/>보다 아래에 있어서, 레인은 항상 요소 밑에 그려집니다.
/// </summary>
public sealed class GodotRenderContext
{
    internal GodotRenderContext(Control root, Func<string, Texture2D?>? textureResolver)
    {
        Root = root;
        TextureResolver = textureResolver;

        RainLayer = CreateContainer("Rain");
        ElementLayer = CreateContainer("Elements");
        root.AddChild(RainLayer);
        root.AddChild(ElementLayer);
    }

    /// <summary>키뷰어 전체의 부모 노드. 좌표는 레이아웃 픽셀 단위입니다.</summary>
    public Control Root { get; }

    /// <summary>레인처럼 모든 요소보다 아래에 그릴 노드를 넣는 곳.</summary>
    public Control RainLayer { get; }

    /// <summary>요소 노드를 넣는 곳. 레이아웃 순서대로 추가되므로 나중 요소가 위에 그려집니다.</summary>
    public Control ElementLayer { get; }

    /// <summary>이미지 키(<c>BoxStyle.SpriteKey</c>)를 텍스처로 바꾸는 함수.</summary>
    public Func<string, Texture2D?>? TextureResolver { get; }

    /// <summary>마우스 입력을 막지 않는 빈 컨테이너 노드를 만듭니다.</summary>
    public static Control CreateContainer(string name) => new()
    {
        Name = name.Replace('/', '_').Replace(':', '_').Replace('.', '_').Replace('@', '_').Replace('%', '_'),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}
