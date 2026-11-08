using MelonLoader;
using Tactile.Adapters.Unity;
using Tactile.Adapters.Unity.Input;
using Tactile.ADOFAI;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Layout.Serialization;
using Tactile.Standalone.Layouts;
using UnityEngine;

[assembly: MelonInfo(typeof(TactileMod), "Tactile", TactileBuildInfo.Version, "bindglam")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace Tactile.ADOFAI;

/// <summary>
/// 얼불춤 화면 위에 Tactile 키뷰어를 띄우는 모드.
/// 설정 창(기본 F5)에서 <c>UserData/Tactile/layouts/</c>의 레이아웃을 고르고, 키뷰어는 마우스로 끌어서 옮깁니다.
/// 설정은 <c>UserData/MelonPreferences.cfg</c>의 [Tactile] 항목에 저장됩니다.
/// </summary>
public sealed class TactileMod : MelonMod
{
    /// <summary>레이아웃이 이 화면 높이일 때 레이아웃 배율 그대로 그림. 화면이 크면 그만큼 키움.</summary>
    private const float ReferenceScreenHeight = 1080f;

    private TactileSettings _settings = null!;
    private SettingsWindow _window = null!;
    private IInputDevice<KeyboardEvent>? _keyboard;
    private TactileUnityViewer? _viewer;
    private KeyViewerLayout? _layout;
    private int _appliedScreenWidth;
    private int _appliedScreenHeight;

    private bool _dragging;
    private Vector2 _dragGrab;

    internal LayoutLibrary Layouts { get; private set; } = null!;

    /// <summary>지금 표시 중인 레이아웃 파일 이름. 기본 레이아웃으로 대신하는 중이면 null.</summary>
    internal string? CurrentLayout { get; private set; }

    /// <summary>마지막으로 레이아웃 폴더·파일을 읽다가 난 오류 (설정 창에 표시).</summary>
    internal string? LayoutError { get; private set; }

    public override void OnInitializeMelon()
    {
        _settings = new TactileSettings();
        _window = new SettingsWindow(this, _settings);
        Layouts = new LayoutLibrary(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Tactile"));

        _keyboard = CreateKeyboard();
        RefreshLayouts();
        string? file = Layouts.Files.FirstOrDefault(f => string.Equals(f, _settings.Layout.Value, StringComparison.OrdinalIgnoreCase))
                       ?? Layouts.Files.FirstOrDefault();
        if (file is null || !SelectLayout(file))
            ShowLayout(DefaultLayouts.FourKeys(), null);
    }

    public override void OnUpdate()
    {
        if (UnityEngine.Input.GetKeyDown(_settings.SettingsKey.Value))
        {
            if (_window.IsOpen)
                _window.Close();
            else
                _window.Open();
        }

        if (_viewer is null)
            return;

        if (UnityEngine.Input.GetKeyDown(_settings.ToggleKey.Value))
            _settings.Visible.Value = !_settings.Visible.Value;

        if (_viewer.Visible != _settings.Visible.Value)
            _viewer.Visible = _settings.Visible.Value;

        if (_appliedScreenWidth != Screen.width || _appliedScreenHeight != Screen.height)
            ApplyPlacement();

        UpdateDrag();
        _viewer.Update();
    }

    public override void OnGUI() => _window.Draw();

    public override void OnPreferencesSaved() => ApplyPlacement();

    public override void OnPreferencesLoaded() => ApplyPlacement();

    public override void OnDeinitializeMelon()
    {
        _viewer?.Dispose();
        _viewer = null;
        _keyboard?.Dispose();
        _keyboard = null;
    }

    /// <summary>레이아웃 폴더를 다시 읽습니다.</summary>
    internal void RefreshLayouts()
    {
        try
        {
            Layouts.Refresh();
            LayoutError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LayoutError = $"레이아웃 폴더를 읽지 못했습니다: {e.Message}";
            LoggerInstance.Error($"{LayoutError} ({Layouts.Directory})");
        }
    }

    /// <summary>레이아웃 파일을 불러와 표시하고 설정에 기억합니다. 파일을 읽지 못하면 지금 레이아웃을 그대로 둡니다.</summary>
    internal bool SelectLayout(string fileName)
    {
        string path = Layouts.GetPath(fileName);
        KeyViewerLayout layout;
        try
        {
            layout = Layouts.Load(fileName);
        }
        catch (Exception e) when (e is LayoutFormatException or IOException or UnauthorizedAccessException)
        {
            LayoutError = $"{fileName}: {e.Message}";
            LoggerInstance.Error($"레이아웃 파일을 읽지 못했습니다 ({path}): {e.Message}");
            return false;
        }

        LayoutError = null;
        LoggerInstance.Msg($"레이아웃을 불러옵니다: {path}");
        ShowLayout(layout, fileName);
        if (_settings.Layout.Value != fileName)
        {
            _settings.Layout.Value = fileName;
            MelonPreferences.Save();
        }

        return true;
    }

    /// <summary>지금 레이아웃 파일을 다시 불러옵니다 (손 표시 설정 변경 등).</summary>
    internal void ReloadLayout()
    {
        if (CurrentLayout is null || !SelectLayout(CurrentLayout))
            ShowLayout(DefaultLayouts.FourKeys(), null);
    }

    /// <summary>위치·크기 설정을 기본값으로 되돌립니다.</summary>
    internal void ResetPlacement()
    {
        _settings.Anchor.Value = _settings.Anchor.DefaultValue;
        _settings.OffsetX.Value = _settings.OffsetX.DefaultValue;
        _settings.OffsetY.Value = _settings.OffsetY.DefaultValue;
        _settings.Scale.Value = _settings.Scale.DefaultValue;
        ApplyPlacement();
    }

    /// <summary>화면 구석 기준 위치와 크기를 적용합니다.</summary>
    internal void ApplyPlacement()
    {
        if (_viewer is null || _layout is null)
            return;

        _appliedScreenWidth = Screen.width;
        _appliedScreenHeight = Screen.height;
        Vector2 corner = CornerVector(_settings.Anchor.Value);

        RectTransform root = _viewer.Root;
        root.anchorMin = root.anchorMax = root.pivot = corner;
        root.anchoredPosition = new Vector2(
            corner.x == 0f ? _settings.OffsetX.Value : -_settings.OffsetX.Value,
            corner.y == 0f ? _settings.OffsetY.Value : -_settings.OffsetY.Value);

        float scale = _layout.Scale * _settings.Scale.Value * Screen.height / ReferenceScreenHeight;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// 플랫폼 키보드를 한 번만 만들어 시작합니다. 레이아웃을 바꿔도 키뷰어만 다시 만들고 장치는 그대로 씁니다.
    /// 전역 입력을 쓸 수 없으면 게임 창 입력(<see cref="UnityLegacyKeyboard"/>)으로 대신합니다.
    /// </summary>
    private IInputDevice<KeyboardEvent> CreateKeyboard()
    {
        IInputDevice<KeyboardEvent> keyboard = TactileUnityViewer.CreatePlatformKeyboard();
        try
        {
            keyboard.Start();
            return keyboard;
        }
        catch (UnauthorizedAccessException e)
        {
            // macOS 입력 모니터링 권한이 없으면, 권한이 생길 때까지 게임 창 입력으로 대신 표시
            LoggerInstance.Warning(e.Message);
            LoggerInstance.Warning("권한을 허용하기 전까지는 게임 창에 포커스가 있을 때의 입력만 표시합니다 (프레임 단위).");
            if (_settings.OpenPermissionSettings.Value)
                InputPermissions.OpenKeyboardSettings();
        }
        catch (InvalidOperationException e)
        {
            // Windows 키보드 훅 설치 실패 등
            LoggerInstance.Warning($"전역 키보드 입력을 시작하지 못했습니다: {e.Message}");
            LoggerInstance.Warning("게임 창에 포커스가 있을 때의 입력만 표시합니다 (프레임 단위).");
        }

        keyboard.Dispose();
        var fallback = new UnityLegacyKeyboard();
        fallback.Start();
        return fallback;
    }

    /// <summary>키뷰어를 새 레이아웃으로 다시 만듭니다.</summary>
    private void ShowLayout(KeyViewerLayout layout, string? fileName)
    {
        if (_keyboard is null)
            return;

        if (!_settings.ShowHands.Value)
            layout.Elements.RemoveAll(e => e is HandsElement);

        _dragging = false;
        _viewer?.Dispose();
        _viewer = null;
        try
        {
            _viewer = new TactileUnityViewer(layout, _keyboard, ownsKeyboard: false);
        }
        catch (Exception e)
        {
            LoggerInstance.Error($"키뷰어를 만들지 못했습니다: {e}");
            _layout = null;
            CurrentLayout = null;
            return;
        }

        _layout = layout;
        CurrentLayout = fileName;
        _viewer.Visible = _settings.Visible.Value;
        ApplyPlacement();
    }

    /// <summary>키뷰어 위에서 왼쪽 버튼을 누른 채 끌면 옮기고, 놓으면 가장 가까운 화면 구석 기준으로 위치를 저장합니다.</summary>
    private void UpdateDrag()
    {
        Vector2 mouse = UnityEngine.Input.mousePosition;
        if (!_dragging)
        {
            if (_settings.DragToMove.Value && _viewer!.Visible && UnityEngine.Input.GetMouseButtonDown(0)
                && !_window.Contains(mouse) && ScreenRect().Contains(mouse))
            {
                _dragging = true;
                _dragGrab = mouse - ScreenRect().min;
            }

            return;
        }

        Rect rect = ScreenRect();
        Vector2 min = mouse - _dragGrab;
        min.x = Mathf.Clamp(min.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
        min.y = Mathf.Clamp(min.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
        Vector2 corner = _viewer!.Root.pivot;
        _viewer.Root.anchoredPosition = min + Vector2.Scale(corner, rect.size) - Vector2.Scale(corner, ScreenSize());

        if (UnityEngine.Input.GetMouseButton(0))
            return;

        _dragging = false;
        SaveDraggedPosition(new Rect(min, rect.size));
    }

    /// <summary>키뷰어 사각형 중심에 가까운 화면 구석을 기준으로 정하고, 그 구석에서의 거리를 설정에 저장합니다.</summary>
    private void SaveDraggedPosition(Rect rect)
    {
        bool right = rect.center.x > Screen.width / 2f;
        bool top = rect.center.y > Screen.height / 2f;
        _settings.Anchor.Value = (right, top) switch
        {
            (false, true) => ScreenCorner.TopLeft,
            (true, true) => ScreenCorner.TopRight,
            (true, false) => ScreenCorner.BottomRight,
            _ => ScreenCorner.BottomLeft,
        };
        _settings.OffsetX.Value = Mathf.Round(right ? Screen.width - rect.xMax : rect.xMin);
        _settings.OffsetY.Value = Mathf.Round(top ? Screen.height - rect.yMax : rect.yMin);
        MelonPreferences.Save(); // OnPreferencesSaved → ApplyPlacement
    }

    /// <summary>키뷰어가 차지하는 화면 사각형 (왼쪽 아래 원점 픽셀, <c>Input.mousePosition</c>과 같은 좌표).</summary>
    private Rect ScreenRect()
    {
        RectTransform root = _viewer!.Root;
        Vector2 size = new Vector2(_layout!.Canvas.X, _layout.Canvas.Y) * root.localScale.x;
        Vector2 pivot = Vector2.Scale(root.anchorMin, ScreenSize()) + root.anchoredPosition;
        return new Rect(pivot - Vector2.Scale(root.pivot, size), size);
    }

    private static Vector2 ScreenSize() => new(Screen.width, Screen.height);

    private static Vector2 CornerVector(ScreenCorner corner) => corner switch
    {
        ScreenCorner.TopLeft => new Vector2(0f, 1f),
        ScreenCorner.TopRight => new Vector2(1f, 1f),
        ScreenCorner.BottomRight => new Vector2(1f, 0f),
        _ => new Vector2(0f, 0f),
    };
}
