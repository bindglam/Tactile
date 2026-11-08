using UnityEngine;

namespace Tactile.ADOFAI;

/// <summary>
/// 설정 창 (IMGUI). <see cref="TactileSettings.SettingsKey"/>(기본 F5)로 열고 닫습니다.
/// 레이아웃 폴더의 파일 목록에서 레이아웃을 고르고, 보이기·크기·손 표시·드래그를 바꿀 수 있습니다.
/// </summary>
internal sealed class SettingsWindow
{
    /// <summary>창 크기는 이 화면 높이 기준이고, 화면이 크면 그만큼 키움.</summary>
    private const float ReferenceScreenHeight = 1080f;

    private const int WindowId = 0x7AC71E;

    private readonly TactileMod _mod;
    private readonly TactileSettings _settings;
    private readonly GUI.WindowFunction _drawWindow;
    private Rect _rect = new(40f, 40f, 420f, 0f);
    private Vector2 _scroll;

    public SettingsWindow(TactileMod mod, TactileSettings settings)
    {
        _mod = mod;
        _settings = settings;
        _drawWindow = DrawWindow;
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        IsOpen = true;
        _mod.RefreshLayouts();
    }

    /// <summary>닫으면서 바뀐 설정을 파일에 저장합니다.</summary>
    public void Close()
    {
        IsOpen = false;
        MelonLoader.MelonPreferences.Save();
    }

    /// <summary>화면 좌표(왼쪽 아래 원점, <c>Input.mousePosition</c>)가 창 위에 있는지.</summary>
    public bool Contains(Vector2 screenPosition)
    {
        if (!IsOpen)
            return false;
        float scale = GuiScale();
        return _rect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y) / scale);
    }

    /// <summary><c>OnGUI</c>에서 호출합니다.</summary>
    public void Draw()
    {
        if (!IsOpen)
            return;

        float scale = GuiScale();
        Matrix4x4 matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        // 높이는 내용에 맞추고(GUILayout), 화면 밖으로 나가지 않게 함
        _rect.height = 0f;
        _rect = GUILayout.Window(WindowId, _rect, _drawWindow, "Tactile 설정");
        _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width / scale - _rect.width));
        _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height / scale - _rect.height));

        GUI.matrix = matrix;
    }

    private static float GuiScale() => Mathf.Max(1f, Screen.height / ReferenceScreenHeight);

    private void DrawWindow(int id)
    {
        GUILayout.Label("레이아웃");
        IReadOnlyList<string> files = _mod.Layouts.Files;
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(220f, 30f * files.Count + 10f)));
        foreach (string file in files)
        {
            bool selected = string.Equals(file, _mod.CurrentLayout, StringComparison.OrdinalIgnoreCase);
            if (GUILayout.Toggle(selected, System.IO.Path.GetFileNameWithoutExtension(file), "Button") && !selected)
                _mod.SelectLayout(file);
        }

        GUILayout.EndScrollView();

        if (_mod.LayoutError is { } error)
            GUILayout.Label($"<color=#ff8080>{error}</color>");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("새로고침"))
            _mod.RefreshLayouts();
        if (GUILayout.Button("폴더 열기"))
            Application.OpenURL(new Uri(_mod.Layouts.Directory).AbsoluteUri);
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        _settings.Visible.Value = GUILayout.Toggle(_settings.Visible.Value, $" 보이기 ({_settings.ToggleKey.Value})");

        bool hands = GUILayout.Toggle(_settings.ShowHands.Value, " 손 표시");
        if (hands != _settings.ShowHands.Value)
        {
            _settings.ShowHands.Value = hands;
            _mod.ReloadLayout();
        }

        _settings.DragToMove.Value = GUILayout.Toggle(_settings.DragToMove.Value, " 마우스로 끌어서 옮기기");

        GUILayout.Label($"크기: {_settings.Scale.Value:0.00}");
        float scale = GUILayout.HorizontalSlider(_settings.Scale.Value, 0.1f, 2f);
        if (!Mathf.Approximately(scale, _settings.Scale.Value))
        {
            _settings.Scale.Value = Mathf.Round(scale * 100f) / 100f;
            _mod.ApplyPlacement();
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("위치 초기화"))
            _mod.ResetPlacement();

        GUILayout.Space(6f);
        if (GUILayout.Button($"닫기 ({_settings.SettingsKey.Value})"))
            Close();

        GUI.DragWindow();
    }
}
