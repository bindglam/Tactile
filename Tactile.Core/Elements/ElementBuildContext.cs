using Tactile.Core.Layout;

namespace Tactile.Core.Elements;

/// <summary>요소 로직을 만들 때 전달되는 정보. 레이아웃 전체 설정 등을 제공합니다.</summary>
public sealed class ElementBuildContext
{
    internal ElementBuildContext(KeyViewerLayout layout, int rainCapacity)
    {
        Layout = layout;
        RainCapacity = rainCapacity;
    }

    /// <summary>레이아웃 전체 설정.</summary>
    public KeyViewerLayout Layout { get; }

    /// <summary>키 하나에 동시에 표시할 수 있는 최대 레인 막대 수.</summary>
    public int RainCapacity { get; }
}
