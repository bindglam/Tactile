using Tactile.Adapters.Unity.Input;
using Tactile.Adapters.Unity.Rendering;
using Tactile.Core;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Input.Keyboard.MacOS;
using Tactile.Core.Layout;
using Tactile.Core.Rendering;
using UnityEngine;

namespace Tactile.Adapters.Unity;

/// <summary>
/// <see cref="KeyViewer"/>를 화면 맨 위의 전용 캔버스(Screen Space - Overlay)에 그립니다.
/// 요소마다 렌더러 등록표(<see cref="TactileUnityOptions.Renderers"/>)에서 찾은 렌더러가 그립니다.
/// <para>
/// 게임의 UI·카메라 구성과 무관하게 동작하도록 UnityEngine.UI 없이 엔진 모듈(<see cref="CanvasRenderer"/>)로만 그립니다.
/// 모드에서는 이 객체를 들고 있다가 매 프레임 <see cref="Update"/>를 호출하면 됩니다.
/// <code>
/// _viewer = new TactileUnityViewer(layout);
/// _viewer.Root.anchoredPosition = new Vector2(20, -20);
/// // 매 프레임
/// _viewer.Update();
/// </code>
/// </para>
/// <para>모든 메서드는 Unity 메인 스레드에서 호출하세요.</para>
/// </summary>
public sealed class TactileUnityViewer : IDisposable
{
    private readonly IInputDevice<KeyboardEvent> _keyboard;
    private readonly bool _ownsKeyboard;
    private readonly GameObject _canvasObject;
    private bool _disposed;

    /// <summary>현재 플랫폼의 키보드 장치(<see cref="CreatePlatformKeyboard"/>)를 만들어 사용합니다. 장치는 이 객체가 시작하고 해제합니다.</summary>
    /// <exception cref="UnauthorizedAccessException">macOS 입력 모니터링 권한이 없는 경우</exception>
    public TactileUnityViewer(KeyViewerLayout layout, TactileUnityOptions? options = null)
        : this(layout, CreatePlatformKeyboard(), ownsKeyboard: true, options)
    {
    }

    /// <summary>직접 만든 입력 장치를 사용합니다.</summary>
    /// <param name="ownsKeyboard">true면 이 객체가 장치를 시작하고 해제합니다.</param>
    public TactileUnityViewer(
        KeyViewerLayout layout, IInputDevice<KeyboardEvent> keyboard, bool ownsKeyboard, TactileUnityOptions? options = null)
    {
        if (layout is null)
            throw new ArgumentNullException(nameof(layout));

        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _ownsKeyboard = ownsKeyboard;
        options ??= new TactileUnityOptions();

        _canvasObject = new GameObject("TactileKeyViewer");
        if (options.KeepAcrossScenes)
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

        Canvas = _canvasObject.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = options.SortingOrder;
        Canvas.pixelPerfect = false;

        // 배율은 루트 하나에만 적용하고, 그 아래는 레이아웃 좌표 그대로 사용
        Root = UnityRenderContext.CreateNode("Root", _canvasObject.transform);
        Root.sizeDelta = new Vector2(layout.Canvas.X, layout.Canvas.Y);
        Root.localScale = new Vector3(layout.Scale, layout.Scale, 1f);

        KeyViewer? viewer = null;
        try
        {
            RendererRegistry<UnityRenderContext> renderers = options.Renderers ?? UnityRenderers.CreateDefault();
            var context = new UnityRenderContext(Root, options);
            viewer = new KeyViewer(layout, keyboard, options.Elements, renderers.Bind(context), options.RainCapacity);
            if (ownsKeyboard)
                keyboard.Start();
        }
        catch
        {
            viewer?.Dispose();
            UnityEngine.Object.Destroy(_canvasObject);
            if (ownsKeyboard)
                keyboard.Dispose();
            throw;
        }

        Viewer = viewer;
    }

    /// <summary>내부 키뷰어. 통계나 상태를 직접 읽을 때 사용합니다.</summary>
    public KeyViewer Viewer { get; }

    /// <summary>키뷰어를 그리는 캔버스.</summary>
    public Canvas Canvas { get; }

    /// <summary>
    /// 키뷰어의 부모. 기준점은 화면 왼쪽 위이며, <c>anchoredPosition</c>으로 옮기고 <c>localScale</c>로 크기를 바꿉니다.
    /// 다른 곳을 기준으로 하려면 <c>anchorMin</c>/<c>anchorMax</c>/<c>pivot</c>을 바꾸세요.
    /// </summary>
    public RectTransform Root { get; }

    /// <summary>보이기/숨기기. 숨겨도 입력과 통계는 계속 갱신됩니다.</summary>
    public bool Visible
    {
        get => Canvas.enabled;
        set => Canvas.enabled = value;
    }

    /// <summary>입력을 반영하고 요소 렌더러들이 화면을 갱신합니다. 매 프레임 호출하세요.</summary>
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
        if (_canvasObject != null)
            UnityEngine.Object.Destroy(_canvasObject);
    }

    /// <summary>
    /// 현재 플랫폼의 키보드 장치를 만듭니다. macOS는 OS 전역 입력(정확한 입력 시각),
    /// 그 밖의 플랫폼은 아직 전용 장치가 없어 Unity 입력을 프레임마다 확인하는 <see cref="UnityLegacyKeyboard"/>를 사용합니다.
    /// </summary>
    public static IInputDevice<KeyboardEvent> CreatePlatformKeyboard() => Application.platform switch
    {
        RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacOSKeyboardDevice(),
        _ => new UnityLegacyKeyboard(),
    };
}
