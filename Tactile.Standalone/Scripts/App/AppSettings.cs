using Godot;

namespace Tactile.Standalone.App;

/// <summary>앱 설정 (마지막으로 연 파일, 표시 창 위치 등). <c>user://settings.cfg</c>에 저장합니다.</summary>
public sealed class AppSettings
{
    private const string Path = "user://settings.cfg";
    private const string Section = "app";

    /// <summary>마지막으로 열거나 저장한 레이아웃 파일 (절대 경로). 없으면 빈 문자열.</summary>
    public string LastLayoutPath { get; set; } = "";

    /// <summary>표시 창의 마지막 위치. 처음이면 null.</summary>
    public Vector2I? OverlayPosition { get; set; }

    /// <summary>표시 창을 항상 다른 창 위에 둘지.</summary>
    public bool OverlayAlwaysOnTop { get; set; } = true;

    /// <summary>에디터 UI 배율 (⌘+ / ⌘−). 1이 기본.</summary>
    public float UiZoom { get; set; } = 1f;

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok)
            return settings;

        settings.LastLayoutPath = (string)config.GetValue(Section, "last_layout_path", "");
        settings.OverlayAlwaysOnTop = (bool)config.GetValue(Section, "overlay_always_on_top", true);
        settings.UiZoom = (float)config.GetValue(Section, "ui_zoom", 1f);
        if (config.HasSectionKey(Section, "overlay_position"))
            settings.OverlayPosition = (Vector2I)config.GetValue(Section, "overlay_position");
        return settings;
    }

    public void Save()
    {
        var config = new ConfigFile();
        config.SetValue(Section, "last_layout_path", LastLayoutPath);
        config.SetValue(Section, "overlay_always_on_top", OverlayAlwaysOnTop);
        config.SetValue(Section, "ui_zoom", UiZoom);
        if (OverlayPosition is { } position)
            config.SetValue(Section, "overlay_position", position);
        config.Save(Path);
    }
}
