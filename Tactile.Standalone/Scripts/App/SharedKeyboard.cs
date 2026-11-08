using System;
using Godot;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Input.Keyboard.MacOS;

namespace Tactile.Standalone.App;

/// <summary>입력 장치 상태.</summary>
public enum KeyboardStatus
{
    /// <summary>입력을 받는 중.</summary>
    Running,

    /// <summary>macOS 입력 모니터링 권한이 없어 기다리는 중 (주기적으로 다시 시도).</summary>
    WaitingForPermission,

    /// <summary>이 운영체제는 아직 지원하지 않음.</summary>
    NotSupported,
}

/// <summary>
/// 앱 전체가 함께 쓰는 키보드 장치. 에디터 미리보기와 표시 창의 키뷰어가 번갈아 쓰고,
/// 에디터는 "키 눌러서 입력 지정"에 같은 입력을 엿봅니다 (<see cref="KeyPressed"/>).
/// 키뷰어에 넘기면 키뷰어는 장치를 시작·해제하지 않으므로, 키뷰어를 다시 만들어도 장치는 계속 살아 있습니다.
/// </summary>
public sealed class SharedKeyboard : IInputDevice<KeyboardEvent>
{
    private const double RetryInterval = 1.0;

    private KeyboardDevice? _device;
    private double _retryTimer;

    /// <summary>키를 누를 때마다 (키뷰어가 입력을 읽는 순간에) 호출됩니다.</summary>
    public event Action<KeyCode>? KeyPressed;

    /// <summary>상태가 바뀌면 호출됩니다.</summary>
    public event Action<KeyboardStatus>? StatusChanged;

    public KeyboardStatus Status { get; private set; } = KeyboardStatus.WaitingForPermission;

    /// <summary>장치를 시작합니다. 권한이 없으면 처음 한 번은 시스템 권한 요청 팝업이 뜹니다. 메인 스레드에서 호출하세요.</summary>
    public void TryStart()
    {
        if (_device is not null)
            return;

        if (!OperatingSystem.IsMacOS())
        {
            SetStatus(KeyboardStatus.NotSupported);
            return;
        }

        var device = new MacOSKeyboardDevice();
        try
        {
            device.Start();
            _device = device;
            SetStatus(KeyboardStatus.Running);
        }
        catch (UnauthorizedAccessException)
        {
            device.Dispose();
            SetStatus(KeyboardStatus.WaitingForPermission);
        }
    }

    /// <summary>권한을 기다리는 중이면 주기적으로 다시 시도합니다. 매 프레임 호출하세요.</summary>
    public void Poll(double delta)
    {
        if (Status != KeyboardStatus.WaitingForPermission)
            return;

        _retryTimer += delta;
        if (_retryTimer < RetryInterval)
            return;
        _retryTimer = 0;

        if (InputPermissions.CheckKeyboard() is InputPermissionStatus.Granted or InputPermissionStatus.NotRequired)
            TryStart();
    }

    /// <summary>쌓인 입력을 버립니다 (키뷰어를 바꿀 때, 그 사이 입력이 한꺼번에 들어가지 않도록).</summary>
    public void Drain()
    {
        while (_device is not null && _device.TryRead(out _))
        {
        }
    }

    // 키뷰어용: 시작·중지는 이 객체가 관리하므로 아무 일도 하지 않음
    void IInputDevice<KeyboardEvent>.Start()
    {
    }

    void IInputDevice<KeyboardEvent>.Stop()
    {
    }

    public bool TryRead(out KeyboardEvent inputEvent)
    {
        if (_device is null || !_device.TryRead(out inputEvent))
        {
            inputEvent = default;
            return false;
        }

        if (inputEvent.Action == KeyAction.Down)
            KeyPressed?.Invoke(inputEvent.Key);
        return true;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
    }

    private void SetStatus(KeyboardStatus status)
    {
        if (Status == status)
            return;
        Status = status;
        StatusChanged?.Invoke(status);
    }
}
