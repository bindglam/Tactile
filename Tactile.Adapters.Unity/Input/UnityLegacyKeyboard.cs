using System.Diagnostics;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using UnityEngine;
using CoreKey = Tactile.Core.Input.Keyboard.KeyCode;
using UnityKey = UnityEngine.KeyCode;

namespace Tactile.Adapters.Unity.Input;

/// <summary>
/// Unity의 기존 입력(<c>UnityEngine.Input</c>)을 프레임마다 확인하는 키보드.
/// 전용 OS 입력 장치가 없는 플랫폼에서 대신 사용합니다. 입력 시각은 프레임 단위로만 정확하고, 게임 창이 포커스를 잃으면 입력을 받지 못합니다.
/// </summary>
public sealed class UnityLegacyKeyboard : IInputDevice<KeyboardEvent>
{
    private static readonly (UnityKey Unity, CoreKey Core)[] Map = BuildMap();

    private readonly bool[] _down = new bool[Map.Length];
    private readonly Queue<KeyboardEvent> _queue = new();
    private int _polledFrame = -1;
    private bool _running;

    public void Start() => _running = true;

    public void Stop() => _running = false;

    public void Dispose()
    {
        _running = false;
        _queue.Clear();
    }

    public bool TryRead(out KeyboardEvent inputEvent)
    {
        Poll();
        if (_queue.Count > 0)
        {
            inputEvent = _queue.Dequeue();
            return true;
        }

        inputEvent = default;
        return false;
    }

    private void Poll()
    {
        if (!_running || _polledFrame == Time.frameCount)
            return;
        _polledFrame = Time.frameCount;

        long now = Stopwatch.GetTimestamp();
        for (int i = 0; i < Map.Length; i++)
        {
            bool down = UnityEngine.Input.GetKey(Map[i].Unity);
            if (down == _down[i])
                continue;
            _down[i] = down;
            _queue.Enqueue(new KeyboardEvent(Map[i].Core, (int)Map[i].Unity, down ? KeyAction.Down : KeyAction.Up, now));
        }
    }

    private static (UnityKey, CoreKey)[] BuildMap()
    {
        var map = new List<(UnityKey, CoreKey)>();
        for (int i = 0; i < 26; i++)
            map.Add((UnityKey.A + i, Core(CoreKey.A, i)));
        map.Add((UnityKey.Alpha0, CoreKey.D0));
        for (int i = 1; i <= 9; i++)
            map.Add((UnityKey.Alpha0 + i, Core(CoreKey.D1, i - 1)));
        for (int i = 0; i < 12; i++)
            map.Add((UnityKey.F1 + i, Core(CoreKey.F1, i)));
        map.Add((UnityKey.Keypad0, CoreKey.Numpad0));
        for (int i = 1; i <= 9; i++)
            map.Add((UnityKey.Keypad0 + i, Core(CoreKey.Numpad1, i - 1)));

        map.AddRange(new[]
        {
            (UnityKey.Return, CoreKey.Enter), (UnityKey.Escape, CoreKey.Escape), (UnityKey.Backspace, CoreKey.Backspace),
            (UnityKey.Tab, CoreKey.Tab), (UnityKey.Space, CoreKey.Space), (UnityKey.Minus, CoreKey.Minus),
            (UnityKey.Equals, CoreKey.Equal), (UnityKey.LeftBracket, CoreKey.LeftBracket),
            (UnityKey.RightBracket, CoreKey.RightBracket), (UnityKey.Backslash, CoreKey.Backslash),
            (UnityKey.Semicolon, CoreKey.Semicolon), (UnityKey.Quote, CoreKey.Quote), (UnityKey.BackQuote, CoreKey.Grave),
            (UnityKey.Comma, CoreKey.Comma), (UnityKey.Period, CoreKey.Period), (UnityKey.Slash, CoreKey.Slash),
            (UnityKey.CapsLock, CoreKey.CapsLock), (UnityKey.Insert, CoreKey.Insert), (UnityKey.Home, CoreKey.Home),
            (UnityKey.PageUp, CoreKey.PageUp), (UnityKey.Delete, CoreKey.Delete), (UnityKey.End, CoreKey.End),
            (UnityKey.PageDown, CoreKey.PageDown), (UnityKey.RightArrow, CoreKey.Right), (UnityKey.LeftArrow, CoreKey.Left),
            (UnityKey.DownArrow, CoreKey.Down), (UnityKey.UpArrow, CoreKey.Up), (UnityKey.Numlock, CoreKey.NumLock),
            (UnityKey.KeypadDivide, CoreKey.NumpadDivide), (UnityKey.KeypadMultiply, CoreKey.NumpadMultiply),
            (UnityKey.KeypadMinus, CoreKey.NumpadSubtract), (UnityKey.KeypadPlus, CoreKey.NumpadAdd),
            (UnityKey.KeypadEnter, CoreKey.NumpadEnter), (UnityKey.KeypadPeriod, CoreKey.NumpadDecimal),
            (UnityKey.LeftControl, CoreKey.LeftControl), (UnityKey.LeftShift, CoreKey.LeftShift),
            (UnityKey.LeftAlt, CoreKey.LeftAlt), (UnityKey.RightControl, CoreKey.RightControl),
            (UnityKey.RightShift, CoreKey.RightShift), (UnityKey.RightAlt, CoreKey.RightAlt),
        });
        return map.ToArray();
    }

    private static CoreKey Core(CoreKey first, int offset) => (CoreKey)((int)first + offset);
}
