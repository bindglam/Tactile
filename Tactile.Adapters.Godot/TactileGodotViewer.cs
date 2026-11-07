using Godot;
using Tactile.Adapters.Godot.Rendering;
using Tactile.Core;
using Tactile.Core.Elements;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Input.Keyboard.MacOS;
using Tactile.Core.Layout;
using Tactile.Core.Rendering;

namespace Tactile.Adapters.Godot;

/// <summary>
/// <see cref="KeyViewer"/>를 Godot 노드로 그립니다. 요소마다 렌더러 등록표(<see cref="TactileGodotOptions.Renderers"/>)에서 찾은 렌더러가 그립니다.
/// <para>
/// Godot는 게임 프로젝트 안의 스크립트만 노드에 붙일 수 있으므로, 이 클래스는 노드가 아니라
/// 게임 쪽 스크립트가 소유하고 호출하는 일반 클래스입니다.
/// <code>
/// public partial class KeyViewerHost : Node2D
/// {
///     private TactileGodotViewer? _viewer;
///     public override void _Ready() => _viewer = new TactileGodotViewer(this, layout);
///     public override void _Process(double delta) => _viewer?.Update();
///     public override void _ExitTree() => _viewer?.Dispose();
/// }
/// </code>
/// </para>
/// <para>
/// 모든 메서드는 Godot 메인 스레드에서 호출하세요.
/// </para>
/// </summary>
public sealed class TactileGodotViewer : IDisposable
{
    private readonly IInputDevice<KeyboardEvent> _keyboard;
    private readonly bool _ownsKeyboard;
    private readonly Control _root;
    private bool _disposed;

    /// <summary>현재 플랫폼의 키보드 장치를 만들어 사용합니다. 장치는 이 객체가 시작하고 해제합니다.</summary>
    /// <param name="parent">키뷰어를 그릴 부모 노드 (Node2D, Control 등 CanvasItem)</param>
    /// <param name="layout">레이아웃</param>
    /// <param name="options">요소·렌더러 등록표, 텍스처 조회 등. null이면 기본값</param>
    /// <exception cref="PlatformNotSupportedException">지원하지 않는 플랫폼</exception>
    /// <exception cref="UnauthorizedAccessException">macOS 입력 모니터링 권한이 없는 경우</exception>
    public TactileGodotViewer(Node parent, KeyViewerLayout layout, TactileGodotOptions? options = null)
        : this(parent, layout, CreatePlatformKeyboard(), ownsKeyboard: true, options)
    {
    }

    /// <summary>
    /// 직접 만든 입력 장치를 사용합니다. 장치의 시작·중지·해제는 호출하는 쪽이 관리합니다.
    /// </summary>
    public TactileGodotViewer(
        Node parent, KeyViewerLayout layout, IInputDevice<KeyboardEvent> keyboard, TactileGodotOptions? options = null)
        : this(parent, layout, keyboard, ownsKeyboard: false, options)
    {
    }

    private TactileGodotViewer(
        Node parent, KeyViewerLayout layout, IInputDevice<KeyboardEvent> keyboard, bool ownsKeyboard,
        TactileGodotOptions? options)
    {
        if (parent is null)
            throw new ArgumentNullException(nameof(parent));

        if (layout is null)
            throw new ArgumentNullException(nameof(layout));

        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _ownsKeyboard = ownsKeyboard;

        // 배율은 루트 하나에만 적용하고, 그 아래는 레이아웃 좌표 그대로 사용 (Godot도 y가 아래로 증가)
        _root = new Control
        {
            Name = "TactileKeyViewer",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Scale = new Vector2(layout.Scale, layout.Scale),
            Size = new Vector2(layout.Canvas.X, layout.Canvas.Y),
        };

        KeyViewer? viewer = null;
        try
        {
            options ??= new TactileGodotOptions();
            RendererRegistry<GodotRenderContext> renderers = options.Renderers ?? GodotRenderers.CreateDefault();
            var context = new GodotRenderContext(_root, options.TextureResolver);
            viewer = new KeyViewer(layout, keyboard, options.Elements, renderers.Bind(context), options.RainCapacity);
            if (ownsKeyboard)
                keyboard.Start();
        }
        catch
        {
            viewer?.Dispose();
            _root.Free();
            if (ownsKeyboard)
                keyboard.Dispose();
            throw;
        }

        Viewer = viewer;
        parent.AddChild(_root);
    }

    /// <summary>내부 키뷰어. 통계나 상태를 직접 읽을 때 사용합니다.</summary>
    public KeyViewer Viewer { get; }

    /// <summary>키뷰어 노드들의 부모. 위치를 옮기거나 숨길 때 사용합니다.</summary>
    public Control Root => _root;

    /// <summary>입력을 반영하고 요소 렌더러들이 노드를 갱신합니다. 매 프레임 <c>_Process</c>에서 호출하세요.</summary>
    public void Update()
    {
        if (!_disposed)
            Viewer.Update();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_ownsKeyboard)
            _keyboard.Dispose();

        Viewer.Dispose();
        if (GodotObject.IsInstanceValid(_root))
            _root.QueueFree();
    }

    private static IInputDevice<KeyboardEvent> CreatePlatformKeyboard()
    {
        if (OperatingSystem.IsMacOS())
            return new MacOSKeyboardDevice();

        throw new PlatformNotSupportedException($"{OS.GetName()}은 아직 지원하지 않습니다.");
    }
}
