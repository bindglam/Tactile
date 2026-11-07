using System.Diagnostics;

namespace Tactile.Core.State;

/// <summary>값이 바뀌는 곡선.</summary>
internal enum Ease
{
    Linear,
    In,     // 천천히 시작
    Out,    // 천천히 끝남
    InOut,
}

/// <summary>
/// 시간에 따라 값 하나를 바꾸는 애니메이션 채널. 지금 진행 중인 구간 하나와, 그것이 끝난 뒤 이어질 구간 하나를 가집니다.
/// (예: 누르는 동작이 끝나기 전에 키를 떼면, 떼는 동작은 누르는 동작이 끝난 뒤 시작)
/// 시각은 <see cref="Stopwatch"/> 틱입니다. 할당하지 않습니다.
/// </summary>
internal struct TweenChannel
{
    private Segment _current;
    private Segment _next;
    private bool _hasNext;

    /// <summary>목표값으로 지금(<paramref name="start"/>) 바로 움직이기 시작합니다. 이어질 구간은 취소됩니다.</summary>
    public void To(float target, long start, float durationMs, Ease ease)
    {
        float from = Evaluate(start);
        _hasNext = false;
        _current = new Segment(from, target, start, Ticks(durationMs), ease);
    }

    /// <summary>지금 구간이 끝난 뒤(이미 끝났으면 바로) 목표값으로 움직입니다.</summary>
    public void After(float target, long start, float durationMs, Ease ease)
    {
        long currentEnd = _current.Start + _current.Duration;
        if (currentEnd > start)
        {
            _next = new Segment(_current.To, target, currentEnd, Ticks(durationMs), ease);
            _hasNext = true;
        }
        else
        {
            To(target, start, durationMs, ease);
        }
    }

    /// <summary>시각 <paramref name="now"/>의 값.</summary>
    public float Evaluate(long now)
    {
        if (_hasNext && now >= _next.Start)
        {
            _current = _next;
            _hasNext = false;
        }

        return _current.Evaluate(now);
    }

    /// <summary>움직임이 모두 끝났는지.</summary>
    public bool IsSettled(long now) => !_hasNext && now >= _current.Start + _current.Duration;

    public void Reset() => this = default;

    private static long Ticks(float ms) => Math.Max(0L, (long)(ms * Stopwatch.Frequency / 1000.0));

    private readonly struct Segment(float from, float to, long start, long duration, Ease ease)
    {
        public readonly float From = from;
        public readonly float To = to;
        public readonly long Start = start;
        public readonly long Duration = duration;
        public readonly Ease Ease = ease;

        public float Evaluate(long now)
        {
            if (Duration <= 0 || now >= Start + Duration)
                return To;
            if (now <= Start)
                return From;

            float p = (float)(now - Start) / Duration;
            p = Ease switch
            {
                Ease.In => p * p,
                Ease.Out => 1f - (1f - p) * (1f - p),
                Ease.InOut => p < 0.5f ? 2f * p * p : 1f - (2f - 2f * p) * (2f - 2f * p) / 2f,
                _ => p,
            };
            return From + (To - From) * p;
        }
    }
}
