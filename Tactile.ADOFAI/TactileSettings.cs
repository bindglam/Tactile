using MelonLoader;
using UnityEngine;

namespace Tactile.ADOFAI;

/// <summary>키뷰어를 놓을 화면 구석.</summary>
public enum ScreenCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>MelonPreferences에 저장되는 설정 (<c>UserData/MelonPreferences.cfg</c>의 [Tactile]).</summary>
internal sealed class TactileSettings
{
    private readonly MelonPreferences_Category _category = MelonPreferences.CreateCategory("Tactile", "Tactile 키뷰어");

    public TactileSettings()
    {
        Visible = _category.CreateEntry("Visible", true, "보이기");
        ToggleKey = _category.CreateEntry("ToggleKey", KeyCode.F8, "보이기/숨기기 키");
        Anchor = _category.CreateEntry("Anchor", ScreenCorner.BottomLeft, "기준 화면 구석");
        OffsetX = _category.CreateEntry("OffsetX", 20f, "구석에서 가로 거리 (픽셀)");
        OffsetY = _category.CreateEntry("OffsetY", 20f, "구석에서 세로 거리 (픽셀)");
        Scale = _category.CreateEntry("Scale", 0.6f, "크기 (1080p 화면 기준)");
        ShowHands = _category.CreateEntry("ShowHands", true, "손 표시 (게임을 다시 시작하면 적용)");
        OpenPermissionSettings = _category.CreateEntry(
            "OpenPermissionSettings", true, "macOS 입력 모니터링 권한이 없으면 설정 열기");
    }

    public MelonPreferences_Entry<bool> Visible { get; }
    public MelonPreferences_Entry<KeyCode> ToggleKey { get; }
    public MelonPreferences_Entry<ScreenCorner> Anchor { get; }
    public MelonPreferences_Entry<float> OffsetX { get; }
    public MelonPreferences_Entry<float> OffsetY { get; }
    public MelonPreferences_Entry<float> Scale { get; }
    public MelonPreferences_Entry<bool> ShowHands { get; }
    public MelonPreferences_Entry<bool> OpenPermissionSettings { get; }
}
