using System;
using Godot;
using Tactile.Adapters.Godot;
using Tactile.Core.Input;
using Tactile.Core.Layout;
using Tactile.Standalone.Layouts;

namespace Tactile.Standalone.Viewer;

/// <summary>
/// 키뷰어를 화면에 표시하는 노드. 레이아웃을 받아 <see cref="TactileGodotViewer"/>를 만들고 매 프레임 갱신합니다.
/// <para>
/// 창의 기준 해상도를 레이아웃 캔버스 크기로 맞추므로, 창 크기를 바꾸면 키뷰어도 같은 비율로 커지고 작아집니다.
/// </para>
/// <para>
/// 입력 권한이 없으면 안내 화면을 띄우고, 권한이 허용될 때까지 주기적으로 확인해서 자동으로 다시 시작합니다.
/// </para>
/// </summary>
public partial class KeyViewerHost : Node2D
{
    /// <summary>권한을 기다리는 동안 다시 확인하는 간격(초).</summary>
    private const double PermissionPollInterval = 1.0;

    private TactileGodotViewer? _viewer;
    private Control? _messagePanel;
    private bool _waitingForPermission;
    private double _pollTimer;

    /// <summary>표시 중인 레이아웃.</summary>
    public KeyViewerLayout? Layout { get; private set; }

    public override void _Ready()
    {
        ShowLayout(DefaultLayouts.FourKeys());
    }

    public override void _Process(double delta)
    {
        if (_viewer is not null)
        {
            _viewer.Update();
            return;
        }

        if (_waitingForPermission && Layout is not null)
        {
            _pollTimer += delta;
            if (_pollTimer >= PermissionPollInterval)
            {
                _pollTimer = 0;
                if (InputPermissions.CheckKeyboard() is InputPermissionStatus.Granted or InputPermissionStatus.NotRequired)
                    ShowLayout(Layout);
            }
        }
    }

    public override void _ExitTree()
    {
        CloseViewer();
    }

    /// <summary>레이아웃을 바꿔서 다시 표시합니다. (나중에 에디터에서 레이아웃을 적용할 때 사용)</summary>
    public void ShowLayout(KeyViewerLayout layout)
    {
        CloseViewer();
        Layout = layout;
        FitWindowTo(layout);

        try
        {
            // 권한을 아직 묻지 않았다면 여기서 시스템 팝업이 뜸 (Core가 처리)
            _viewer = new TactileGodotViewer(this, layout);
        }
        catch (UnauthorizedAccessException)
        {
            _waitingForPermission = true;
            _pollTimer = 0;
            ShowPermissionMessage(layout);
        }
        catch (Exception e)
        {
            GD.PushError($"[Tactile] 키뷰어를 시작하지 못했습니다: {e}");
            ShowMessage(layout, DescribeError(e), buttonText: null, onPressed: null);
        }
    }

    private void CloseViewer()
    {
        _viewer?.Dispose();
        _viewer = null;
        _waitingForPermission = false;

        _messagePanel?.QueueFree();
        _messagePanel = null;
    }

    /// <summary>창의 기준 해상도와 처음 크기를 레이아웃 캔버스에 맞춥니다.</summary>
    private void FitWindowTo(KeyViewerLayout layout)
    {
        var canvas = new Vector2I(
            Math.Max(1, (int)Math.Ceiling(layout.Canvas.X * layout.Scale)),
            Math.Max(1, (int)Math.Ceiling(layout.Canvas.Y * layout.Scale)));

        Window window = GetWindow();
        window.ContentScaleSize = canvas;

        // 헤드리스 실행 등 창이 없는 환경에서는 크기 조정을 건너뜀
        if (DisplayServer.GetName() != "headless")
            window.Size = canvas;
    }

    private void ShowPermissionMessage(KeyViewerLayout layout)
    {
        bool denied = InputPermissions.CheckKeyboard() == InputPermissionStatus.Denied;

        string message = denied
            ? "키 입력을 읽을 권한이 꺼져 있습니다.\n\n" +
              "시스템 설정 → 개인정보 보호 및 보안 → 입력 모니터링에서 이 앱을 켜 주세요.\n\n" +
              "허용하면 자동으로 시작합니다."
            : "키 입력을 읽을 권한이 필요합니다.\n\n" +
              "팝업에서 허용해 주세요.\n" +
              "팝업이 보이지 않으면 아래 버튼으로 설정을 열어 주세요.\n\n" +
              "허용하면 자동으로 시작합니다.";

        ShowMessage(layout, message, "설정 열기", () => InputPermissions.OpenKeyboardSettings());
    }

    private void ShowMessage(KeyViewerLayout layout, string message, string? buttonText, Action? onPressed)
    {
        var panel = new CenterContainer
        {
            Position = Vector2.Zero,
            Size = new Vector2(layout.Canvas.X, layout.Canvas.Y),
        };

        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(Math.Max(100f, layout.Canvas.X - 40f), 0f),
        };
        box.AddThemeConstantOverride("separation", 16);
        panel.AddChild(box);

        var label = new Label
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        box.AddChild(label);

        if (buttonText is not null && onPressed is not null)
        {
            var button = new Button
            {
                Text = buttonText,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            button.Pressed += onPressed;
            box.AddChild(button);
        }

        _messagePanel = panel;
        AddChild(panel);
    }

    private static string DescribeError(Exception e) => e switch
    {
        PlatformNotSupportedException => $"이 운영체제({OS.GetName()})는 아직 지원하지 않습니다.",
        _ => $"키뷰어를 시작하지 못했습니다.\n\n{e.Message}",
    };
}
