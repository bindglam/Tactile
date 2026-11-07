namespace Tactile.Core.Input;

/// <summary>
/// 입력 장치. 별도 스레드에서 OS 입력을 수집해 큐에 쌓아 두고,
/// 사용하는 쪽은 원하는 시점(보통 매 프레임)에 <see cref="TryRead"/>로 꺼내 씁니다.
/// </summary>
public interface IInputDevice<TEvent> : IDisposable
    where TEvent : IInputEvent
{
    /// <summary>입력 수집을 시작합니다.</summary>
    void Start();

    /// <summary>입력 수집을 멈춥니다. 큐에 남은 이벤트는 계속 읽을 수 있습니다.</summary>
    void Stop();

    /// <summary>
    /// 큐에서 이벤트를 하나 꺼냅니다. 꺼낼 이벤트가 없으면 기다리지 않고 false를 반환합니다.
    /// 한 스레드에서만 호출하세요.
    /// </summary>
    bool TryRead(out TEvent inputEvent);
}
