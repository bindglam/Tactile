namespace Tactile.Core.State;

/// <summary>키 요소 하나의 실행 중 상태. 시각은 모두 <see cref="System.Diagnostics.Stopwatch"/> 틱입니다.</summary>
public sealed class KeyState
{
    internal KeyState(int rainCapacity)
    {
        Rain = new RainBuffer(rainCapacity);
    }

    /// <summary>연결된 입력 중 지금 눌려 있는 개수.</summary>
    public int HeldBindings { get; internal set; }

    /// <summary>연결된 입력 중 하나라도 눌려 있는지.</summary>
    public bool IsPressed => HeldBindings > 0;

    /// <summary>누른 횟수. 연결된 입력을 누를 때마다 1씩 늘어납니다.</summary>
    public long PressCount { get; internal set; }

    /// <summary>마지막으로 눌린 상태가 된 시각.</summary>
    public long LastDownAt { get; internal set; }

    /// <summary>마지막으로 떼어진 상태가 된 시각.</summary>
    public long LastUpAt { get; internal set; }

    /// <summary>
    /// 스타일 전환 진행도 (0 = 평소, 1 = 눌림). 누르면 즉시 1이 되고, 떼면 스타일의 전환 시간에 걸쳐 0으로 돌아갑니다.
    /// </summary>
    public float PressAmount { get; internal set; }

    /// <summary>이 키의 레인 막대들.</summary>
    public RainBuffer Rain { get; }

    /// <summary>마지막 Advance 이후 눌린 적이 있는지 (한 프레임보다 짧은 입력도 최소 한 프레임은 보이게 하기 위함).</summary>
    internal bool PressedSinceAdvance { get; set; }
}
