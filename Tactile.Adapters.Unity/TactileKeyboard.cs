using Tactile.Core.Input.Keyboard;
using Tactile.Core.Input.Keyboard.MacOS;
using Tactile.Core.Input.Keyboard.Windows;
using UnityEngine;

namespace Tactile.Adapters.Unity;

/// <summary>
/// 전역 키보드 입력을 매 프레임 수집하는 컴포넌트입니다.
/// <para>
/// 다른 스크립트의 <c>Update()</c>보다 먼저 실행되어, 같은 프레임에서 <see cref="FrameEvents"/>를 읽을 수 있습니다.
/// 각 이벤트의 <see cref="KeyboardEvent.Timestamp"/>는 프레임 시각이 아니라 실제 입력 시각입니다.
/// </para>
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class TactileKeyboard : MonoBehaviour
{
    // 매 프레임 재사용 (Mono GC 부담을 줄이기 위해 새로 할당하지 않음)
    private readonly List<KeyboardEvent> _frameEvents = new();
    private KeyboardDevice? _device;

    /// <summary>이번 프레임에 들어온 키 이벤트 (입력 순서).</summary>
    public IReadOnlyList<KeyboardEvent> FrameEvents => _frameEvents;

    /// <summary>큐가 넘쳐서 버려진 이벤트 수. 늘었다면 눌림 상태를 초기화하는 것이 좋습니다.</summary>
    public long DroppedEventCount => _device?.DroppedEventCount ?? 0;

    private void OnEnable()
    {
        try
        {
            _device = CreateDevice();
            _device.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Tactile] 키보드 입력 수집을 시작하지 못했습니다: {e.Message}");
            _device?.Dispose();
            _device = null;
            enabled = false;
        }
    }

    private void Update()
    {
        _frameEvents.Clear();
        if (_device is null)
            return;

        while (_device.TryRead(out KeyboardEvent e))
            _frameEvents.Add(e);
    }

    private void OnDisable()
    {
        _device?.Dispose();
        _device = null;
        _frameEvents.Clear();
    }

    private static KeyboardDevice CreateDevice() => Application.platform switch
    {
        RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacOSKeyboardDevice(),
        RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsKeyboardDevice(),
        _ => throw new PlatformNotSupportedException($"{Application.platform}은 아직 지원하지 않습니다."),
    };
}
