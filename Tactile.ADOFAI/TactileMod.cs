using MelonLoader;
using Tactile.Adapters.Unity;
using Tactile.Adapters.Unity.Input;
using Tactile.ADOFAI;
using Tactile.Core.Input;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Serialization;
using Tactile.Standalone.Layouts;
using UnityEngine;

[assembly: MelonInfo(typeof(TactileMod), "Tactile", TactileBuildInfo.Version, "bindglam")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace Tactile.ADOFAI;

/// <summary>
/// 얼불춤 화면 위에 Tactile 키뷰어를 띄우는 모드.
/// 설정은 <c>UserData/MelonPreferences.cfg</c>의 [Tactile] 항목에서 바꿀 수 있습니다.
/// </summary>
public sealed class TactileMod : MelonMod
{
    /// <summary>레이아웃이 이 화면 높이일 때 레이아웃 배율 그대로 그림. 화면이 크면 그만큼 키움.</summary>
    private const float ReferenceScreenHeight = 1080f;

    private TactileSettings _settings = null!;
    private TactileUnityViewer? _viewer;
    private KeyViewerLayout? _layout;
    private int _appliedScreenHeight;

    public override void OnInitializeMelon()
    {
        _settings = new TactileSettings();
        CreateViewer();
    }

    public override void OnUpdate()
    {
        if (_viewer is null)
            return;

        if (UnityEngine.Input.GetKeyDown(_settings.ToggleKey.Value))
            _settings.Visible.Value = !_settings.Visible.Value;

        if (_viewer.Visible != _settings.Visible.Value)
            _viewer.Visible = _settings.Visible.Value;

        if (_appliedScreenHeight != Screen.height)
            ApplyPlacement();

        _viewer.Update();
    }

    public override void OnPreferencesSaved() => ApplyPlacement();

    public override void OnPreferencesLoaded() => ApplyPlacement();

    public override void OnDeinitializeMelon()
    {
        _viewer?.Dispose();
        _viewer = null;
    }

    /// <summary>
    /// <c>UserData/Tactile/layout.json</c>을 읽습니다. 없으면 기본 레이아웃을 저장해 두고(직접 고쳐 쓸 수 있도록) 사용합니다.
    /// 파일이 잘못되어 있으면 파일은 그대로 두고 기본 레이아웃을 사용합니다.
    /// </summary>
    private KeyViewerLayout LoadLayout()
    {
        string path = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Tactile", "layout.json");
        try
        {
            if (System.IO.File.Exists(path))
            {
                LoggerInstance.Msg($"레이아웃을 불러옵니다: {path}");
                return LayoutSerializer.LoadFile(path);
            }

            KeyViewerLayout layout = DefaultLayouts.FourKeys();
            LayoutSerializer.SaveFile(layout, path);
            LoggerInstance.Msg($"기본 레이아웃을 저장했습니다: {path}");
            return layout;
        }
        catch (Exception e) when (e is LayoutFormatException or System.IO.IOException or UnauthorizedAccessException)
        {
            LoggerInstance.Error($"레이아웃 파일을 읽지 못해 기본 레이아웃을 사용합니다 ({path}): {e.Message}");
            return DefaultLayouts.FourKeys();
        }
    }

    private void CreateViewer()
    {
        _layout = LoadLayout();
        if (!_settings.ShowHands.Value)
            _layout.Elements.RemoveAll(e => e is Core.Layout.Elements.HandsElement);

        try
        {
            _viewer = new TactileUnityViewer(_layout);
            LoggerInstance.Msg("키뷰어를 시작했습니다.");
        }
        catch (UnauthorizedAccessException e)
        {
            // macOS 입력 모니터링 권한이 없으면, 권한이 생길 때까지 게임 창 입력으로 대신 표시
            LoggerInstance.Warning(e.Message);
            LoggerInstance.Warning("권한을 허용하기 전까지는 게임 창에 포커스가 있을 때의 입력만 표시합니다 (프레임 단위).");
            if (_settings.OpenPermissionSettings.Value)
                InputPermissions.OpenKeyboardSettings();
            _viewer = new TactileUnityViewer(_layout, new UnityLegacyKeyboard(), ownsKeyboard: true);
        }
        catch (Exception e)
        {
            LoggerInstance.Error($"키뷰어를 만들지 못했습니다: {e}");
            return;
        }

        ApplyPlacement();
    }

    /// <summary>화면 구석 기준 위치와 크기를 적용합니다.</summary>
    private void ApplyPlacement()
    {
        if (_viewer is null || _layout is null)
            return;

        _appliedScreenHeight = Screen.height;
        Vector2 corner = _settings.Anchor.Value switch
        {
            ScreenCorner.TopLeft => new Vector2(0f, 1f),
            ScreenCorner.TopRight => new Vector2(1f, 1f),
            ScreenCorner.BottomRight => new Vector2(1f, 0f),
            _ => new Vector2(0f, 0f),
        };

        RectTransform root = _viewer.Root;
        root.anchorMin = root.anchorMax = root.pivot = corner;
        root.anchoredPosition = new Vector2(
            corner.x == 0f ? _settings.OffsetX.Value : -_settings.OffsetX.Value,
            corner.y == 0f ? _settings.OffsetY.Value : -_settings.OffsetY.Value);

        float scale = _layout.Scale * _settings.Scale.Value * Screen.height / ReferenceScreenHeight;
        root.localScale = new Vector3(scale, scale, 1f);
    }
}
