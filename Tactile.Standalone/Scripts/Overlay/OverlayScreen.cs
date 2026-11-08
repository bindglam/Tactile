using System;
using Godot;
using Tactile.Adapters.Godot;
using Tactile.Core.Layout;
using Tactile.Standalone.App;

namespace Tactile.Standalone.Overlay;

/// <summary>
/// 표시 모드 (<c>Scenes/Overlay.tscn</c>): 배경이 투명한 창에 키뷰어만 그립니다.
/// <list type="bullet">
/// <item>끌기: 창 이동</item>
/// <item>우클릭: 메뉴 (편집으로 돌아가기, 항상 위에 표시, 종료)</item>
/// <item>더블클릭 또는 Esc: 편집으로 돌아가기</item>
/// </list>
/// </summary>
public sealed partial class OverlayScreen : Control
{
    private const int MenuEdit = 0, MenuTop = 1, MenuQuit = 2;

    private SharedKeyboard _keyboard = null!;

    // Overlay.tscn의 노드
    private PopupMenu _menu = null!;
    private Control _viewerRoot = null!;
    private Label _error = null!;
    private TactileGodotViewer? _viewer;

    private bool _dragging;
    private Vector2I _dragMouse;
    private Vector2I _dragWindow;

    public override void _Ready()
    {
        _menu = GetNode<PopupMenu>("%Menu");
        _viewerRoot = GetNode<Control>("%Viewer");
        _error = GetNode<Label>("%Error");
        _menu.IdPressed += OnMenu;
    }

    public void Initialize(SharedKeyboard keyboard) => _keyboard = keyboard;

    /// <summary>편집으로 돌아가기.</summary>
    public event Action? EditRequested;

    /// <summary>"항상 위에 표시"를 바꿈.</summary>
    public event Action<bool>? AlwaysOnTopChanged;

    /// <summary>종료.</summary>
    public event Action? QuitRequested;

    /// <summary>표시를 시작합니다. 키뷰어는 레이아웃 배율을 그대로 씁니다.</summary>
    public void Activate(KeyViewerLayout layout, bool alwaysOnTop)
    {
        Visible = true;
        _menu.SetItemChecked(_menu.GetItemIndex(MenuTop), alwaysOnTop);
        _keyboard.Drain();
        try
        {
            _viewer = new TactileGodotViewer(_viewerRoot, layout, _keyboard);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            // 레이아웃이 잘못되어도 창은 띄워서 돌아갈 수 있게 함
            _error.Text = $"표시할 수 없습니다: {e.Message}\n더블클릭해서 편집으로 돌아가세요.";
            _error.Visible = true;
        }

        GrabFocus();
    }

    public void Deactivate()
    {
        Visible = false;
        _dragging = false;
        _viewer?.Dispose();
        _viewer = null;
        _error.Visible = false;
    }

    public override void _Process(double delta) => _viewer?.Update();

    public override void _ExitTree()
    {
        _viewer?.Dispose();
        _viewer = null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, DoubleClick: true }:
                _dragging = false;
                EditRequested?.Invoke();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                // 창 이동: 화면 좌표로 계산 (창이 움직이면 창 안 좌표가 함께 바뀌므로)
                _dragging = button.Pressed;
                _dragMouse = DisplayServer.MouseGetPosition();
                _dragWindow = GetWindow().Position;
                break;

            case InputEventMouseMotion when _dragging:
                GetWindow().Position = _dragWindow + DisplayServer.MouseGetPosition() - _dragMouse;
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                _menu.Position = DisplayServer.MouseGetPosition();
                _menu.Popup();
                break;

            default:
                return;
        }

        AcceptEvent();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            EditRequested?.Invoke();
        }
    }

    private void OnMenu(long id)
    {
        switch (id)
        {
            case MenuEdit:
                EditRequested?.Invoke();
                break;
            case MenuTop:
                int index = _menu.GetItemIndex(MenuTop);
                bool on = !_menu.IsItemChecked(index);
                _menu.SetItemChecked(index, on);
                AlwaysOnTopChanged?.Invoke(on);
                break;
            case MenuQuit:
                QuitRequested?.Invoke();
                break;
        }
    }
}
