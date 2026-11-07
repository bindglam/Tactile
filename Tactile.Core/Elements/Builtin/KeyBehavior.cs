using System.Diagnostics;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using Tactile.Core.State;

namespace Tactile.Core.Elements.Builtin;

/// <summary>기본 키 요소의 로직: 눌림 상태, 누른 횟수, 눌림 모양 전환, 레인.</summary>
public sealed class KeyBehavior : ElementBehavior<KeyElement, KeyVisual>
{
    private readonly InputBinding[] _bindings;
    private readonly KeyStyle _style;
    private readonly RainSettings _rain;
    private readonly bool _hasRain;

    public KeyBehavior(KeyElement element, ElementBuildContext context)
        : base(element)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        _bindings = (element.Bindings ?? []).Distinct().ToArray();
        // 실행 중 원본이 바뀌어도 영향받지 않도록 복사
        _style = element.Style?.Clone() ?? new KeyStyle();
        _rain = element.Rain;
        _hasRain = _rain.Enabled && _rain.Speed > 0f && _rain.MaxLength > 0f;

        State = new KeyState(context.RainCapacity);
        Visual = new KeyVisual(element, _style, _rain, _hasRain, context.RainCapacity);
    }

    public override IReadOnlyList<InputBinding> Bindings => _bindings;

    /// <summary>실행 중 상태.</summary>
    public KeyState State { get; }

    public override KeyVisual Visual { get; }

    public override void OnInput(in KeyboardEvent e)
    {
        if (!IsBound(e.Key))
            return;

        KeyState key = State;
        if (e.Action == KeyAction.Down)
        {
            key.PressCount++;
            key.PressedSinceAdvance = true;
            key.PressAmount = 1f;

            if (key.HeldBindings++ == 0)
            {
                key.LastDownAt = e.Timestamp;
                if (_hasRain)
                    key.Rain.Begin(e.Timestamp);
            }
        }
        else if (key.HeldBindings > 0 && --key.HeldBindings == 0)
        {
            Release(e.Timestamp);
        }
    }

    public override void Update(in FrameContext frame)
    {
        KeyState key = State;

        if (key.IsPressed || key.PressedSinceAdvance)
        {
            // 눌린 동안, 그리고 이번 프레임에 눌렸다 떼어진 짧은 입력도 최소 한 프레임은 눌린 모양으로 보임
            key.PressAmount = 1f;
        }
        else if (key.PressAmount > 0f)
        {
            double transitionTicks = _style.TransitionMs * Stopwatch.Frequency / 1000.0;
            key.PressAmount = transitionTicks <= 0
                ? 0f
                : (float)Math.Max(0.0, key.PressAmount - frame.ElapsedTicks / transitionTicks);
        }

        key.PressedSinceAdvance = false;

        if (_hasRain)
        {
            long lifetimeTicks = (long)(_rain.MaxLength / _rain.Speed * Stopwatch.Frequency);
            key.Rain.RemoveExpired(frame.Now, lifetimeTicks);
        }

        Visual.Update(key, frame.Now);
    }

    public override void ReleaseAll(long now)
    {
        if (State.HeldBindings == 0)
            return;
        State.HeldBindings = 0;
        Release(now);
    }

    public override void Reset()
    {
        KeyState key = State;
        key.HeldBindings = 0;
        key.PressCount = 0;
        key.LastDownAt = 0;
        key.LastUpAt = 0;
        key.PressAmount = 0f;
        key.PressedSinceAdvance = false;
        key.Rain.Clear();
    }

    private bool IsBound(KeyCode key)
    {
        // 바인딩은 보통 1~2개라 선형 탐색이 사전보다 빠름
        var binding = InputBinding.Keyboard(key);
        foreach (InputBinding b in _bindings)
        {
            if (b == binding)
                return true;
        }

        return false;
    }

    private void Release(long timestamp)
    {
        State.LastUpAt = timestamp;
        State.Rain.End(timestamp);
    }
}
