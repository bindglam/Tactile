using System.Diagnostics;
using Tactile.Core.State;

namespace Tactile.Core.Elements;

/// <summary><see cref="ElementBehavior.Update"/>에 전달되는 프레임 정보.</summary>
public readonly struct FrameContext
{
    internal FrameContext(long now, long elapsedTicks, InputStatistics statistics)
    {
        Now = now;
        ElapsedTicks = elapsedTicks;
        Statistics = statistics;
    }

    /// <summary>현재 시각 (<see cref="Stopwatch"/> 틱).</summary>
    public long Now { get; }

    /// <summary>지난 프레임 이후 흐른 시간 (<see cref="Stopwatch"/> 틱). 첫 프레임은 0입니다.</summary>
    public long ElapsedTicks { get; }

    /// <summary>지난 프레임 이후 흐른 시간(초).</summary>
    public double ElapsedSeconds => (double)ElapsedTicks / Stopwatch.Frequency;

    /// <summary>레이아웃 전체의 입력 통계. 이번 프레임까지 반영되어 있습니다.</summary>
    public InputStatistics Statistics { get; }
}
