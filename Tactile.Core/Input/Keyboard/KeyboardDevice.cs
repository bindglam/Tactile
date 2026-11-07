using System.Runtime.ExceptionServices;

namespace Tactile.Core.Input.Keyboard;

/// <summary>
/// 플랫폼별 키보드 장치의 공통 기반 클래스입니다.
/// <para>
/// <b>수집 스레드</b>에서 플랫폼 구현이 OS 입력을 받아 <see cref="Enqueue"/>로 큐에 넣고,
/// 사용하는 쪽은 원하는 스레드(보통 엔진 메인 스레드)에서 매 프레임 <see cref="TryRead"/>로 꺼냅니다.
/// <code>
/// while (device.TryRead(out var e)) { ... }
/// </code>
/// </para>
/// <para>
/// 큐에 <see cref="MaxPendingEvents"/>개보다 많이 쌓이면 가장 오래된 이벤트부터 버리고
/// <see cref="DroppedEventCount"/>를 늘립니다. (일시정지나 로딩 중 <see cref="TryRead"/>를 한동안 호출하지 않는 경우 등)
/// </para>
/// <para>
/// 파생 클래스는 수집 스레드에서 호출되는 <see cref="InitializeCapture"/> → <see cref="RunCapture"/>
/// → <see cref="CleanupCapture"/>와, 다른 스레드에서 호출되는 <see cref="RequestStopCapture"/>를 구현합니다.
/// </para>
/// </summary>
public abstract class KeyboardDevice : IInputDevice<KeyboardEvent>
{
    /// <summary>큐에 쌓아 둘 수 있는 이벤트 수의 기본값.</summary>
    public const int DefaultMaxPendingEvents = 8192;

    /// <summary><see cref="Stop"/>이 수집 스레드 종료를 기다리는 최대 시간.</summary>
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private readonly object _gate = new();

    private Thread? _captureThread;
    private volatile EventQueue? _queue;
    private volatile bool _stopRequested;
    private bool _disposed;

    // TryRead를 호출하는 스레드에서만 사용
    private Queue<RawKeyEvent> _batch = new();
    private KeyResolver _resolver;

    protected KeyboardDevice(int maxPendingEvents)
    {
        if (maxPendingEvents <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingEvents), "1 이상이어야 합니다.");

        MaxPendingEvents = maxPendingEvents;
        _resolver = new KeyResolver(this);
    }

    /// <summary>큐에 쌓아 둘 수 있는 최대 이벤트 수. 넘치면 가장 오래된 것부터 버립니다.</summary>
    public int MaxPendingEvents { get; }

    /// <summary>
    /// 마지막 <see cref="Start"/> 이후 큐가 넘쳐서 버려진 이벤트 수.
    /// 값이 늘었다면 키 눌림 상태가 어긋났을 수 있으니, 사용하는 쪽에서 상태를 초기화하는 것이 좋습니다.
    /// </summary>
    public long DroppedEventCount => _queue?.DroppedCount ?? 0;

    /// <summary><see cref="Stop"/>이 호출되었는지. <see cref="RunCapture"/> 루프의 종료 조건으로 사용합니다.</summary>
    protected bool IsStopRequested => _stopRequested;

    /// <summary>
    /// 입력 수집을 시작합니다. 메인 스레드에서 호출하는 것을 권장합니다.
    /// <para>
    /// 호출한 스레드에서 <see cref="PrepareStart"/>(권한 확인 등)를 실행한 뒤 수집 스레드를 띄우고,
    /// 수집 준비(<see cref="InitializeCapture"/>)가 끝날 때까지 기다립니다.
    /// 준비 중 발생한 예외(권한 없음 등)는 여기서 그대로 다시 던집니다.
    /// 이전 실행에서 읽지 않고 남은 이벤트는 버립니다.
    /// </para>
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
            if (_captureThread is not null)
                return;

            // 권한 팝업, OS API 준비처럼 메인 스레드에서 해야 안전한 작업
            PrepareStart();

            _stopRequested = false;

            var queue = new EventQueue(MaxPendingEvents);
            _batch = new Queue<RawKeyEvent>();
            _resolver = new KeyResolver(this);
            _queue = queue;

            using var ready = new ManualResetEventSlim();
            Exception? startError = null;

            var captureThread = new Thread(() => Capture(ready, e => startError = e))
            {
                Name = $"Tactile.{GetType().Name}.Capture",
                IsBackground = true,
            };
            captureThread.Start();
            ready.Wait();

            if (startError is not null)
            {
                captureThread.Join();
                queue.Complete();
                _queue = null;
                ExceptionDispatchInfo.Capture(startError).Throw();
            }

            _captureThread = captureThread;
        }
    }

    /// <summary>
    /// 입력 수집을 멈추고 수집 스레드가 끝날 때까지 기다립니다. (최대 <see cref="StopTimeout"/>)
    /// 큐에 남은 이벤트는 다음 <see cref="Start"/> 전까지 <see cref="TryRead"/>로 계속 읽을 수 있습니다.
    /// <para>
    /// 수집 스레드가 OS 호출 안에서 멈춰 제시간에 끝나지 않으면 기다리지 않고 반환합니다.
    /// 수집 스레드는 백그라운드 스레드라 프로세스 종료를 막지 않습니다.
    /// </para>
    /// </summary>
    public void Stop()
    {
        Thread? captureThread;
        EventQueue? queue;
        lock (_gate)
        {
            captureThread = _captureThread;
            if (captureThread is null)
                return;

            _stopRequested = true;
            RequestStopCapture();

            queue = _queue;
            _captureThread = null;
        }

        // 정상이라면 이 시점 이후로는 큐에 쓰는 쪽이 없음. 시간 초과 시에도 큐를 닫아 이후 쓰기는 무시됨
        captureThread.Join(StopTimeout);
        queue?.Complete();
    }

    /// <summary>
    /// 큐에서 이벤트를 하나 꺼냅니다. 꺼낼 이벤트가 없으면 기다리지 않고 false를 반환합니다.
    /// <para>
    /// 한 스레드에서만 호출하세요. 보통 엔진의 메인 스레드에서 매 프레임 false가 나올 때까지 반복 호출합니다.
    /// 락은 큐에 쌓인 이벤트를 한꺼번에 가져올 때만 잡고, 호출 중 메모리를 할당하지 않습니다.
    /// </para>
    /// </summary>
    public bool TryRead(out KeyboardEvent keyboardEvent)
    {
        while (true)
        {
            if (_batch.Count > 0)
            {
                keyboardEvent = _resolver.Resolve(_batch.Dequeue());
                return true;
            }

            EventQueue? queue = _queue;
            if (queue is null || !queue.TrySwap(ref _batch))
            {
                keyboardEvent = default;
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Stop();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 수집 스레드에서 호출됩니다. 수집 준비(OS 훅 등록 등)를 하고, 실패하면 예외를 던집니다.
    /// 예외가 나면 <see cref="CleanupCapture"/>가 호출된 뒤 <see cref="Start"/>에서 다시 던져집니다.
    /// </summary>
    protected abstract void InitializeCapture();

    /// <summary>
    /// 수집 스레드에서 호출됩니다. <see cref="IsStopRequested"/>가 true가 되거나
    /// <see cref="RequestStopCapture"/>가 호출될 때까지 블록하며 입력을 받아 <see cref="Enqueue"/>합니다.
    /// <para>
    /// <see cref="RequestStopCapture"/>는 이 메서드에 진입하기 직전에 호출될 수도 있으므로,
    /// 블록하기 전에 <see cref="IsStopRequested"/>를 확인하거나 중지 요청을 놓치지 않게 구현하세요.
    /// </para>
    /// </summary>
    protected abstract void RunCapture();

    /// <summary>
    /// <see cref="Stop"/>을 호출한 스레드에서 호출됩니다. <see cref="RunCapture"/>가 반환하도록 신호만 보내고 바로 반환하세요.
    /// </summary>
    protected abstract void RequestStopCapture();

    /// <summary>
    /// 수집 스레드에서 호출됩니다. <see cref="InitializeCapture"/>가 일부만 성공한 상태에서도 호출될 수 있습니다.
    /// </summary>
    protected abstract void CleanupCapture();

    /// <summary><see cref="TryRead"/>를 호출한 스레드에서 호출됩니다. OS 키코드를 공용 키코드로 변환합니다.</summary>
    protected abstract KeyCode ResolveKey(int rawCode);

    /// <summary>
    /// <see cref="Start"/>를 호출한 스레드(보통 메인 스레드)에서, 수집 스레드를 띄우기 전에 호출됩니다.
    /// 권한 확인·요청, 변환표 미리 만들기처럼 메인 스레드에서 해야 안전한 작업을 합니다.
    /// 예외를 던지면 <see cref="Start"/>가 그대로 다시 던집니다.
    /// </summary>
    protected virtual void PrepareStart()
    {
    }

    /// <summary>
    /// 수집 스레드에서 호출합니다. 큐에 넣기만 하고 바로 반환합니다 (블록되지 않음).
    /// </summary>
    /// <param name="rawCode">OS 키코드</param>
    /// <param name="action">눌림/뗌</param>
    /// <param name="timestamp">입력 발생 시각 (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> 단위)</param>
    protected void Enqueue(int rawCode, KeyAction action, long timestamp) =>
        _queue?.Enqueue(new RawKeyEvent(rawCode, action, timestamp));

    private void Capture(ManualResetEventSlim ready, Action<Exception> reportError)
    {
        try
        {
            InitializeCapture();
        }
        catch (Exception e)
        {
            try { CleanupCapture(); } catch { /* 초기화 예외를 우선 전달 */ }
            reportError(e);
            ready.Set();
            return;
        }

        ready.Set();

        try
        {
            if (!_stopRequested)
                RunCapture();
        }
        catch
        {
            // 백그라운드 스레드 예외는 프로세스를 종료시키므로 삼킴 (수집만 멈춤)
        }
        finally
        {
            CleanupCapture();
        }
    }

    private readonly struct RawKeyEvent(int code, KeyAction action, long timestamp)
    {
        public readonly int Code = code;
        public readonly KeyAction Action = action;
        public readonly long Timestamp = timestamp;
    }

    /// <summary>
    /// 원시 이벤트를 <see cref="KeyboardEvent"/>로 변환합니다. 한 스레드에서만 사용합니다.
    /// 누르는 도중 키보드 배열이 바뀌어도 Down/Up이 같은 KeyCode로 짝지어지도록 Down 때 해석한 값을 보관합니다.
    /// </summary>
    private sealed class KeyResolver(KeyboardDevice owner)
    {
        private readonly Dictionary<int, KeyCode> _resolvedKeys = new();

        public KeyboardEvent Resolve(RawKeyEvent raw)
        {
            KeyCode key;
            if (raw.Action == KeyAction.Down)
            {
                key = SafeResolve(raw.Code);
                _resolvedKeys[raw.Code] = key;
            }
            else if (_resolvedKeys.TryGetValue(raw.Code, out key))
            {
                _resolvedKeys.Remove(raw.Code);
            }
            else
            {
                key = SafeResolve(raw.Code);
            }

            return new KeyboardEvent(key, raw.Code, raw.Action, raw.Timestamp);
        }

        private KeyCode SafeResolve(int rawCode)
        {
            try
            {
                return owner.ResolveKey(rawCode);
            }
            catch
            {
                return KeyCode.Unknown;
            }
        }
    }

    /// <summary>
    /// 생산자 하나, 소비자 하나용 큐. 소비자는 쌓인 항목을 큐째로 바꿔 가져가므로
    /// 락을 잡는 시간이 항목 수와 무관하게 짧습니다.
    /// </summary>
    private sealed class EventQueue(int capacity)
    {
        private readonly object _sync = new();
        private Queue<RawKeyEvent> _items = new();
        private bool _completed;
        private long _droppedCount;

        public long DroppedCount => Interlocked.Read(ref _droppedCount);

        public void Enqueue(RawKeyEvent item)
        {
            lock (_sync)
            {
                if (_completed)
                    return;

                if (_items.Count >= capacity)
                {
                    _items.Dequeue(); // 가장 오래된 것부터 버림
                    Interlocked.Increment(ref _droppedCount);
                }

                _items.Enqueue(item);
            }
        }

        public void Complete()
        {
            lock (_sync)
                _completed = true;
        }

        /// <summary>항목이 있으면 비어 있는 <paramref name="batch"/>와 내부 큐를 맞바꿉니다. 기다리지 않습니다.</summary>
        public bool TrySwap(ref Queue<RawKeyEvent> batch)
        {
            lock (_sync)
            {
                if (_items.Count == 0)
                    return false;

                (_items, batch) = (batch, _items);
                return true;
            }
        }
    }
}
