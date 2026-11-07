using System.Diagnostics;
using Tactile.Core.Elements;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using Tactile.Core.State;

namespace Tactile.Core;

/// <summary>
/// 키뷰어 진입점. 레이아웃의 요소마다 로직(<see cref="ElementBehavior"/>)과 렌더러를 만들고,
/// 매 프레임 입력을 전달한 뒤 계산한 값을 렌더러에 넘겨 그리게 합니다.
/// <code>
/// var elements = ElementRegistry.CreateDefault();          // 애드온 요소는 여기에 등록
/// using var viewer = new KeyViewer(layout, keyboard, elements, renderers.Bind(context));
/// keyboard.Start();
///
/// // 매 프레임 (엔진 메인 스레드)
/// viewer.Update();
/// </code>
/// <para>
/// 등록되지 않은 종류의 요소는 건너뛰고 <see cref="UnsupportedElements"/>에 모아 둡니다 (애드온이 빠진 레이아웃 등).
/// 렌더러가 없는 요소는 로직만 실행되고 그려지지 않습니다.
/// 입력 장치의 시작·중지·해제는 호출하는 쪽이 관리합니다. 한 스레드에서만 사용하세요.
/// </para>
/// </summary>
public sealed class KeyViewer : IDisposable
{
    /// <summary>키 하나에 동시에 표시할 수 있는 레인 막대 수의 기본값.</summary>
    public const int DefaultRainCapacity = RainBuffer.DefaultCapacity;

    private readonly IInputDevice<KeyboardEvent> _keyboard;
    private readonly ElementBehavior[] _elements;
    private readonly IElementView?[] _views;
    private readonly HashSet<InputBinding> _boundInputs = new();
    private readonly HashSet<KeyCode> _held = new();
    private long _lastDroppedCount;
    private long _lastUpdateAt;
    private bool _hasUpdated;
    private bool _disposed;

    /// <param name="layout">레이아웃. 생성 시점에 검사·변환되며, 이후 원본을 바꿔도 반영되지 않습니다.</param>
    /// <param name="keyboard">키보드 입력 장치</param>
    /// <param name="elements">요소 종류 등록표. null이면 <see cref="ElementRegistry.CreateDefault"/></param>
    /// <param name="renderers">렌더러. null이면 값만 계산합니다 (<see cref="Elements"/>에서 직접 읽어 그려도 됨).</param>
    /// <param name="rainCapacity">키 하나에 동시에 표시할 수 있는 최대 레인 막대 수</param>
    /// <exception cref="ArgumentException">요소 ID 중복 등 레이아웃이 올바르지 않은 경우</exception>
    public KeyViewer(
        KeyViewerLayout layout,
        IInputDevice<KeyboardEvent> keyboard,
        ElementRegistry? elements = null,
        IElementViewProvider? renderers = null,
        int rainCapacity = DefaultRainCapacity)
    {
        if (layout is null)
            throw new ArgumentNullException(nameof(layout));
        if (rainCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(rainCapacity), "1 이상이어야 합니다.");

        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        Layout = layout;

        var unsupported = new List<LayoutElement>();
        _elements = CreateElements(layout, elements ?? ElementRegistry.CreateDefault(), rainCapacity, unsupported);
        UnsupportedElements = unsupported;

        foreach (ElementBehavior element in _elements)
        {
            foreach (InputBinding binding in element.Bindings)
                _boundInputs.Add(binding);
        }

        _views = new IElementView?[_elements.Length];
        if (renderers is not null)
            CreateViews(renderers);
    }

    /// <summary>원본 레이아웃.</summary>
    public KeyViewerLayout Layout { get; }

    /// <summary>요소 로직 (레이아웃 순서). 등록되지 않은 종류의 요소는 빠져 있습니다.</summary>
    public IReadOnlyList<ElementBehavior> Elements => _elements;

    /// <summary>등록되지 않은 종류라서 건너뛴 요소.</summary>
    public IReadOnlyList<LayoutElement> UnsupportedElements { get; }

    /// <summary>레이아웃 전체의 입력 통계 (요소에 연결된 입력만 집계).</summary>
    public InputStatistics Statistics { get; } = new();

    /// <summary>현재 시각으로 <see cref="Update(long)"/>을 호출합니다.</summary>
    public void Update() => Update(Stopwatch.GetTimestamp());

    /// <summary>
    /// 쌓인 입력을 모든 요소에 전달하고, 시간 흐름을 반영한 뒤 렌더러를 호출합니다.
    /// </summary>
    /// <param name="now">현재 시각 (<see cref="Stopwatch"/> 틱)</param>
    public void Update(long now)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KeyViewer));

        // 큐가 넘쳐 이벤트가 유실됐다면 눌림 상태가 어긋났을 수 있으므로 모두 뗀 상태에서 다시 시작
        if (_keyboard is KeyboardDevice device)
        {
            long dropped = device.DroppedEventCount;
            if (dropped != _lastDroppedCount)
            {
                _lastDroppedCount = dropped;
                if (dropped > 0)
                    ReleaseAll(now);
            }
        }

        while (_keyboard.TryRead(out KeyboardEvent e))
            Dispatch(e);

        Statistics.Advance(now);

        long elapsed = _hasUpdated ? Math.Max(0, now - _lastUpdateAt) : 0;
        _lastUpdateAt = now;
        _hasUpdated = true;

        var frame = new FrameContext(now, elapsed, Statistics);
        for (int i = 0; i < _elements.Length; i++)
        {
            _elements[i].Update(frame);
            _views[i]?.Render();
        }
    }

    /// <summary>
    /// 모든 입력을 뗀 상태로 만듭니다. 누른 횟수와 통계는 유지합니다.
    /// (입력 이벤트가 유실되어 눌림 상태가 어긋났을 때 사용)
    /// </summary>
    public void ReleaseAll(long now)
    {
        _held.Clear();
        foreach (ElementBehavior element in _elements)
            element.ReleaseAll(now);
    }

    /// <summary>누른 횟수, 통계, 레인까지 모두 처음 상태로 되돌립니다.</summary>
    public void Reset()
    {
        _held.Clear();
        Statistics.Reset();
        foreach (ElementBehavior element in _elements)
            element.Reset();
    }

    /// <summary>렌더러를 모두 해제합니다. 입력 장치는 해제하지 않습니다.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeViews();
    }

    private void Dispatch(in KeyboardEvent e)
    {
        if (e.Key == KeyCode.Unknown)
            return;

        // 같은 키가 연달아 눌리거나(Up 유실) 눌린 적 없는 키가 떼어지는 이벤트는 요소에 전달하지 않음
        if (e.Action == KeyAction.Down)
        {
            if (!_held.Add(e.Key))
                return;
            if (_boundInputs.Contains(InputBinding.Keyboard(e.Key)))
                Statistics.RecordPress(e.Timestamp);
        }
        else if (!_held.Remove(e.Key))
        {
            return;
        }

        foreach (ElementBehavior element in _elements)
            element.OnInput(e);
    }

    private static ElementBehavior[] CreateElements(
        KeyViewerLayout layout, ElementRegistry registry, int rainCapacity, List<LayoutElement> unsupported)
    {
        if (layout.Version > KeyViewerLayout.CurrentVersion)
            throw new ArgumentException($"지원하지 않는 레이아웃 버전입니다: {layout.Version} (최대 {KeyViewerLayout.CurrentVersion})");

        var context = new ElementBuildContext(layout, rainCapacity);
        var result = new List<ElementBehavior>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        IList<LayoutElement> elements = layout.Elements ?? [];
        for (int i = 0; i < elements.Count; i++)
        {
            LayoutElement element = elements[i] ?? throw new ArgumentException($"{i}번째 요소가 null입니다.");

            if (!string.IsNullOrEmpty(element.Id) && !ids.Add(element.Id))
                throw new ArgumentException($"요소 ID가 중복됩니다: {element.Id}");

            ElementBehavior? behavior = registry.Create(element, context);
            if (behavior is null)
                unsupported.Add(element);
            else
                result.Add(behavior);
        }

        return result.ToArray();
    }

    private void CreateViews(IElementViewProvider provider)
    {
        try
        {
            // 레이아웃 순서대로 생성 → 엔진은 나중에 만든 요소를 위에 그리면 됨
            for (int i = 0; i < _elements.Length; i++)
                _views[i] = provider.CreateView(_elements[i]);
        }
        catch
        {
            DisposeViews();
            throw;
        }
    }

    private void DisposeViews()
    {
        for (int i = 0; i < _views.Length; i++)
        {
            _views[i]?.Dispose();
            _views[i] = null;
        }
    }
}
