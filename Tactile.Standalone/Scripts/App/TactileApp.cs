using System;
using System.IO;
using Godot;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Serialization;
using Tactile.Standalone.Editor;
using Tactile.Standalone.Layouts;
using Tactile.Standalone.Overlay;

namespace Tactile.Standalone.App;

/// <summary>
/// 앱 본체 (메인 씬 <c>Scenes/Main.tscn</c>, 자식으로 Editor·Overlay 씬). 처음에는 에디터를 보여 주고, "표시"를 누르면 메인 창을 테두리 없는 투명한 키뷰어 창으로 바꿉니다.
/// <para>
/// Godot은 메인 창을 숨길 수 없으므로, 창을 하나 더 띄우는 대신 메인 창 자체를 바꿉니다:
/// 편집 중에는 불투명한 일반 창, 표시 중에는 테두리 없음·배경 투명·키뷰어 크기의 창. 돌아오면 원래 크기와 위치로 되돌립니다.
/// (창 투명도를 쓰려면 project.godot의 <c>display/window/per_pixel_transparency/allowed</c>와 <c>display/window/size/transparent</c>가 켜져 있어야 함)
/// </para>
/// </summary>
public sealed partial class TactileApp : Control
{
    // 에디터 창 크기 (화면 논리 px). Godot 창 크기는 실제 픽셀이라 화면 배율을 곱해서 씀
    private static readonly Vector2I EditorMinSize = new(960, 600);
    private static readonly Vector2I EditorDefaultSize = new(1280, 800);

    // 에디터 UI 배율
    private const float MaxAutoZoom = 2f;
    private const float MinUiZoom = 0.6f, MaxUiZoom = 2.5f, UiZoomStep = 0.1f;

    private readonly SharedKeyboard _keyboard = new();
    private AppSettings _settings = null!;
    private LayoutDocument _document = null!;
    private EditorScreen _editor = null!;
    private OverlayScreen _overlay = null!;

    private bool _overlayMode;
    private Vector2I _editorPosition;
    private Vector2I _editorSize;
    private Window.ModeEnum _editorMode;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = false; // 저장하지 않은 변경을 물어보기 위해

        _settings = AppSettings.Load();
        _keyboard.TryStart();

        (KeyViewerLayout layout, string path, string? error) = LoadInitialLayout();
        _document = new LayoutDocument(layout, path);

        _editor = GetNode<EditorScreen>("Editor");
        _editor.Initialize(_document, _keyboard);
        _editor.ShowRequested += EnterOverlay;
        _editor.TitleChanged += UpdateTitle;
        _editor.LayoutOpened += p =>
        {
            _settings.LastLayoutPath = p;
            _settings.Save();
        };

        _overlay = GetNode<OverlayScreen>("Overlay");
        _overlay.Initialize(_keyboard);
        _overlay.EditRequested += ExitOverlay;
        _overlay.QuitRequested += RequestQuit;
        _overlay.AlwaysOnTopChanged += on =>
        {
            _settings.OverlayAlwaysOnTop = on;
            _settings.Save();
            GetWindow().AlwaysOnTop = on;
        };

        Window window = GetWindow();
        window.SizeChanged += UpdateContentScale;
        Vector2I minSize = Physical(EditorMinSize);
        window.MinSize = minSize;
        if (window.Size.X < minSize.X * 1.2f || window.Size.Y < minSize.Y * 1.2f)
        {
            window.Size = Physical(EditorDefaultSize);
            window.MoveToCenter();
        }

        UpdateContentScale();
        GetViewport().TransparentBg = false;
        _editor.Activate();
        UpdateTitle();

        if (error is not null)
            CallDeferred(MethodName.ShowLoadError, error);
    }

    public override void _Process(double delta) => _keyboard.Poll(delta);

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            RequestQuit();
    }

    public override void _ExitTree() => _keyboard.Dispose();

    // ---- 표시 모드 전환 ----

    private void EnterOverlay()
    {
        if (_overlayMode)
            return;
        _overlayMode = true;

        Window window = GetWindow();
        _editorMode = window.Mode;
        if (window.Mode != Window.ModeEnum.Windowed)
            window.Mode = Window.ModeEnum.Windowed;
        _editorPosition = window.Position;
        _editorSize = window.Size;

        _editor.Deactivate();
        KeyViewerLayout layout = _document.Layout;

        // 창: 테두리 없음, 투명, 키뷰어 크기 (레이아웃 px = 화면 논리 px)
        GetViewport().TransparentBg = true;
        window.MinSize = Vector2I.One;
        window.Borderless = true;
        window.AlwaysOnTop = _settings.OverlayAlwaysOnTop;
        float uiScale = UiScale();
        window.Size = new Vector2I(
            Math.Max(1, (int)Math.Ceiling(layout.Canvas.X * layout.Scale * uiScale)),
            Math.Max(1, (int)Math.Ceiling(layout.Canvas.Y * layout.Scale * uiScale)));
        window.Position = _settings.OverlayPosition is { } saved && IsOnScreen(saved, window.Size) ? saved : _editorPosition;
        UpdateContentScale();

        _overlay.Activate(layout, _settings.OverlayAlwaysOnTop);
        UpdateTitle();
    }

    private void ExitOverlay()
    {
        if (!_overlayMode)
            return;
        _overlayMode = false;

        Window window = GetWindow();
        _settings.OverlayPosition = window.Position;
        _settings.Save();

        _overlay.Deactivate();
        GetViewport().TransparentBg = false;
        window.AlwaysOnTop = false;
        window.Borderless = false;
        window.Size = _editorSize;
        window.MinSize = Physical(EditorMinSize);
        window.Position = _editorPosition;
        if (_editorMode != Window.ModeEnum.Windowed)
            window.Mode = _editorMode;
        UpdateContentScale();

        _editor.Activate();
        UpdateTitle();
    }

    /// <summary>화면 배율(Retina 등)에 맞춰 UI를 키웁니다. 레이아웃 1px = 화면 논리 1px.</summary>
    private void UpdateContentScale()
    {
        Window window = GetWindow();
        float scale = ContentScale();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        window.ContentScaleSize = new Vector2I(
            Math.Max(1, (int)Math.Round(window.Size.X / scale)),
            Math.Max(1, (int)Math.Round(window.Size.Y / scale)));
    }

    /// <summary>
    /// 화면에 그리는 배율.
    /// <list type="bullet">
    /// <item>표시 창: 화면 배율만 (레이아웃 1px = 화면 논리 1px)</item>
    /// <item>에디터: 화면 배율 × 창 크기에 따른 배율 × 사용자 배율(⌘+ / ⌘−)</item>
    /// </list>
    /// 창 크기에 따른 배율은 기본 크기(1280×800)보다 클 때만 창 크기에 비례해 커집니다 (작게 줄여도 글자는 작아지지 않음).
    /// </summary>
    private float ContentScale()
    {
        float screen = UiScale();
        if (_overlayMode)
            return screen;

        Vector2I size = GetWindow().Size;
        float fit = Math.Min(size.X / screen / EditorDefaultSize.X, size.Y / screen / EditorDefaultSize.Y);
        float auto = Math.Clamp(fit, 1f, MaxAutoZoom);
        return screen * auto * _settings.UiZoom;
    }

    /// <summary>사용자 UI 배율을 바꿉니다 (⌘+ / ⌘− / ⌘0).</summary>
    private void SetUiZoom(float zoom)
    {
        zoom = (float)Math.Round(Math.Clamp(zoom, MinUiZoom, MaxUiZoom), 2);
        if (Math.Abs(zoom - _settings.UiZoom) < 0.001f)
            return;
        _settings.UiZoom = zoom;
        _settings.Save();
        UpdateContentScale();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_overlayMode || @event is not InputEventKey { Pressed: true } key || !key.IsCommandOrControlPressed())
            return;

        switch (key.Keycode)
        {
            case Key.Equal or Key.Plus or Key.KpAdd:
                SetUiZoom(_settings.UiZoom + UiZoomStep);
                break;
            case Key.Minus or Key.KpSubtract:
                SetUiZoom(_settings.UiZoom - UiZoomStep);
                break;
            case Key.Key0 or Key.Kp0:
                SetUiZoom(1f);
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private Vector2I Physical(Vector2I logical) => new((int)(logical.X * UiScale()), (int)(logical.Y * UiScale()));

    private float UiScale() => Math.Max(1f, DisplayServer.ScreenGetScale(GetWindow().CurrentScreen));

    private static bool IsOnScreen(Vector2I position, Vector2I size)
    {
        for (int i = 0; i < DisplayServer.GetScreenCount(); i++)
        {
            var screen = new Rect2I(DisplayServer.ScreenGetPosition(i), DisplayServer.ScreenGetSize(i));
            if (screen.Intersects(new Rect2I(position, size)))
                return true;
        }

        return false;
    }

    private void UpdateTitle()
    {
        GetWindow().Title = _overlayMode
            ? "Tactile"
            : $"{Path.GetFileName(_document.Path)}{(_document.IsDirty ? " •" : "")} — Tactile";
    }

    // ---- 시작, 종료 ----

    /// <summary>
    /// 처음 열 레이아웃: 마지막으로 연 파일 → 기본 위치의 layout.json → (없으면) 기본 레이아웃을 그 자리에 저장.
    /// </summary>
    private (KeyViewerLayout Layout, string Path, string? Error) LoadInitialLayout()
    {
        string path = File.Exists(_settings.LastLayoutPath) ? _settings.LastLayoutPath : DefaultLayoutPath();
        try
        {
            if (File.Exists(path))
                return (LayoutSerializer.LoadFile(path), path, null);

            KeyViewerLayout layout = DefaultLayouts.FourKeys();
            LayoutSerializer.SaveFile(layout, path);
            GD.Print($"[Tactile] 기본 레이아웃을 저장했습니다: {path}");
            return (layout, path, null);
        }
        catch (Exception e) when (e is LayoutFormatException or IOException or UnauthorizedAccessException)
        {
            GD.PushError($"[Tactile] 레이아웃을 읽지 못했습니다 ({path}): {e.Message}");
            return (DefaultLayouts.FourKeys(), path, $"{path}\n\n{e.Message}\n\n기본 레이아웃을 열었습니다. 저장하면 이 파일을 덮어씁니다.");
        }
    }

    /// <summary>기본 레이아웃 파일: Godot 에디터에서 실행하면 프로젝트 폴더, 내보낸 앱이면 사용자 데이터 폴더.</summary>
    private static string DefaultLayoutPath() =>
        OS.HasFeature("editor")
            ? ProjectSettings.GlobalizePath("res://layout.json")
            : ProjectSettings.GlobalizePath("user://layout.json");

    private void ShowLoadError(string message) => _editor.Alert("레이아웃을 열 수 없습니다", message);

    private void RequestQuit()
    {
        if (_overlayMode)
            ExitOverlay();
        _editor.ConfirmDiscard(() => GetTree().Quit());
    }
}
