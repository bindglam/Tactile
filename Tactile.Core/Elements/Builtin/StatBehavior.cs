using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;

namespace Tactile.Core.Elements.Builtin;

/// <summary>기본 통계 요소의 로직: 통계 값을 형식에 맞춰 글자로 만듭니다.</summary>
public sealed class StatBehavior : ElementBehavior<StatElement, StatVisual>
{
    public StatBehavior(StatElement element, ElementBuildContext context)
        : base(element)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        Visual = new StatVisual(element);
    }

    public override StatVisual Visual { get; }

    public override void Update(in FrameContext frame) => Visual.Update(frame.Statistics);
}
