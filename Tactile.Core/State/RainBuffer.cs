namespace Tactile.Core.State;

/// <summary>레인 막대 하나. 시각은 <see cref="System.Diagnostics.Stopwatch"/> 틱입니다.</summary>
public readonly struct RainSegment(long startAt, long endAt, bool isOpen)
{
    /// <summary>키를 누른 시각.</summary>
    public long StartAt { get; } = startAt;

    /// <summary>키를 뗀 시각. <see cref="IsOpen"/>이면 의미 없음.</summary>
    public long EndAt { get; } = endAt;

    /// <summary>아직 누르고 있는 중인지.</summary>
    public bool IsOpen { get; } = isOpen;
}

/// <summary>
/// 키 하나의 레인 막대들을 담는 고정 크기 원형 버퍼. 생성 후에는 메모리를 할당하지 않습니다.
/// <para>
/// 막대마다 버퍼 안의 칸 번호(slot)가 살아 있는 동안 바뀌지 않으므로, 렌더러가 노드를 재사용하는 키로 쓸 수 있습니다.
/// 가득 차면 가장 오래된 막대를 버립니다.
/// </para>
/// </summary>
public sealed class RainBuffer
{
    /// <summary>키 하나에 동시에 표시할 수 있는 최대 막대 수의 기본값.</summary>
    public const int DefaultCapacity = 64;

    private readonly RainSegment[] _items;
    private int _head;   // 가장 오래된 막대의 칸
    private int _count;

    public RainBuffer(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new RainSegment[capacity];
    }

    public int Capacity => _items.Length;

    public int Count => _count;

    /// <summary>
    /// 오래된 순서로 <paramref name="order"/>번째 막대를 가져옵니다. <paramref name="slot"/>은 버퍼 안의 칸 번호입니다.
    /// </summary>
    public RainSegment Get(int order, out int slot)
    {
        if ((uint)order >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(order));
        slot = (_head + order) % _items.Length;
        return _items[slot];
    }

    /// <summary>새 막대를 시작합니다.</summary>
    public void Begin(long startAt)
    {
        if (_count == _items.Length)
        {
            _head = (_head + 1) % _items.Length;
            _count--;
        }

        _items[(_head + _count) % _items.Length] = new RainSegment(startAt, 0, isOpen: true);
        _count++;
    }

    /// <summary>진행 중인 막대를 끝냅니다.</summary>
    public void End(long endAt)
    {
        if (_count == 0)
            return;

        int last = (_head + _count - 1) % _items.Length;
        RainSegment segment = _items[last];
        if (segment.IsOpen)
            _items[last] = new RainSegment(segment.StartAt, Math.Max(endAt, segment.StartAt), isOpen: false);
    }

    /// <summary>끝난 지 <paramref name="lifetimeTicks"/> 이상 지나 화면 밖으로 완전히 나간 막대를 지웁니다.</summary>
    public void RemoveExpired(long now, long lifetimeTicks)
    {
        while (_count > 0)
        {
            RainSegment oldest = _items[_head];
            if (oldest.IsOpen || now - oldest.EndAt < lifetimeTicks)
                break;

            _head = (_head + 1) % _items.Length;
            _count--;
        }
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }
}
