using System.Diagnostics;

namespace Tactile.Core.State;

/// <summary>
/// 입력 통계. KPS는 최근 <see cref="WindowTicks"/> 동안 누른 횟수를 초당 값으로 환산한 것입니다.
/// 생성 후에는 메모리를 할당하지 않습니다.
/// </summary>
public sealed class InputStatistics
{
    // 윈도우 안의 입력 시각. 1초에 이보다 많이 누르면 오래된 것부터 버려져 KPS가 낮게 잡힘
    private const int Capacity = 2048;

    private readonly long[] _times = new long[Capacity];
    private int _head;
    private int _count;

    public InputStatistics()
        : this(Stopwatch.Frequency)
    {
    }

    /// <param name="windowTicks">KPS를 계산할 시간 범위 (<see cref="Stopwatch"/> 틱)</param>
    public InputStatistics(long windowTicks)
    {
        if (windowTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowTicks));
        WindowTicks = windowTicks;
    }

    /// <summary>KPS를 계산할 시간 범위 (<see cref="Stopwatch"/> 틱). 기본 1초.</summary>
    public long WindowTicks { get; }

    /// <summary>초당 누른 횟수 (최근 <see cref="WindowTicks"/> 기준).</summary>
    public double Kps { get; private set; }

    /// <summary>지금까지의 최대 KPS.</summary>
    public double MaxKps { get; private set; }

    /// <summary>지금까지 누른 총 횟수.</summary>
    public long Total { get; private set; }

    internal void RecordPress(long timestamp)
    {
        Total++;

        if (_count == Capacity)
        {
            _head = (_head + 1) % Capacity;
            _count--;
        }

        _times[(_head + _count) % Capacity] = timestamp;
        _count++;
    }

    internal void Advance(long now)
    {
        long windowStart = now - WindowTicks;
        while (_count > 0 && _times[_head] <= windowStart)
        {
            _head = (_head + 1) % Capacity;
            _count--;
        }

        Kps = _count * ((double)Stopwatch.Frequency / WindowTicks);
        if (Kps > MaxKps)
            MaxKps = Kps;
    }

    internal void Reset()
    {
        _head = 0;
        _count = 0;
        Kps = 0;
        MaxKps = 0;
        Total = 0;
    }
}
