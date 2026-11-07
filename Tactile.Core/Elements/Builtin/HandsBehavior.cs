using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using Tactile.Core.State;

namespace Tactile.Core.Elements.Builtin;

/// <summary>
/// 손 요소의 로직. ADOFAI Macro 3(CLiF-1593, MIT) 손 캠의 애니메이션을 실시간 입력에 맞게 옮긴 것입니다.
/// <list type="bullet">
/// <item>손가락: 누르면 뿌리 마디가 <see cref="HandsElement.PressAngle"/>만큼 내려가고(Ease Out), 떼면 돌아옴.
/// 누르는 동작이 끝나기 전에 떼도 끝까지 누른 뒤 돌아오므로, 아주 짧은 입력도 보입니다.</item>
/// <item>손: 그 손의 손가락이 하나도 눌려 있지 않을 때 누르면 "한 번의 타건 묶음"이 시작됩니다.
/// 손이 내려가고(작아짐 10%, 숙임 10도), 묶음의 첫 손가락과 지금 손가락에 따라 좌우로 돌며 옆으로 밀립니다.
/// 그 손의 손가락을 모두 떼면 원래 자세로 돌아옵니다.</item>
/// </list>
/// 원작은 앞으로 칠 입력을 미리 알아 누르기 전에 준비 동작을 하지만, 키뷰어는 모든 동작을 누른 순간부터 시작합니다.
/// 통계(KPS 등)에는 영향을 주지 않습니다.
/// </summary>
public sealed class HandsBehavior : ElementBehavior<HandsElement, HandsVisual>
{
    // 원작 손 단위 움직임 (HandCam::Init)
    private const float PressZoom = -0.1f;   // 누를 때 손 크기 (멀어지는 효과)
    private const float PressUpDown = -10f;  // 누를 때 숙임
    private const float Basement = 100f;     // 손바닥 기준 길이 (옆 이동 계산)

    private readonly Dictionary<InputBinding, int> _fingerOf = new();
    private readonly HashSet<InputBinding> _heldInputs = new();
    private readonly int[] _held = new int[HandsVisual.FingerCount];

    private readonly TweenChannel[] _finger = new TweenChannel[HandsVisual.FingerCount];
    private readonly TweenChannel[] _zoom = new TweenChannel[2];
    private readonly TweenChannel[] _upDown = new TweenChannel[2];
    private readonly TweenChannel[] _leftRight = new TweenChannel[2];
    private readonly TweenChannel[] _xOffset = new TweenChannel[2];
    private readonly int[] _heldInHand = new int[2];
    private readonly int[] _groupFirst = [-1, -1]; // 묶음의 첫 손가락 (원작 번호: 엄지 -1, 검지 0 ~ 새끼 3)

    private readonly float[] _angles = new float[HandsVisual.FingerCount];
    private readonly float[] _press = new float[HandsVisual.FingerCount];

    public HandsBehavior(HandsElement element)
        : base(element)
    {
        foreach (FingerBinding binding in element.Fingers ?? [])
        {
            if ((uint)binding.Finger >= HandsVisual.FingerCount)
                throw new ArgumentException($"손 요소 '{element.Id}'에 잘못된 손가락이 있습니다: {binding.Finger}");

            // 같은 입력이 여러 번 있으면 마지막 연결을 사용
            _fingerOf[binding.Input] = (int)binding.Finger;
        }

        Visual = new HandsVisual(element);
    }

    public override HandsVisual Visual { get; }

    /// <summary>손가락의 눌림 정도 (0 ~ 1).</summary>
    public float GetPressAmount(Finger finger) => _press[(int)finger];

    /// <summary>손가락이 지금 눌려 있는지 (연결된 키 중 하나라도 눌림).</summary>
    public bool IsHeld(Finger finger) => _held[(int)finger] > 0;

    public override void OnInput(in KeyboardEvent e)
    {
        var input = InputBinding.Keyboard(e.Key);
        if (!_fingerOf.TryGetValue(input, out int finger))
            return;

        if (e.Action == KeyAction.Down)
        {
            if (_heldInputs.Add(input))
                Press(finger, e.Timestamp);
        }
        else if (_heldInputs.Remove(input) && _held[finger] > 0)
        {
            Release(finger, e.Timestamp);
        }
    }

    public override void Update(in FrameContext frame)
    {
        long now = frame.Now;
        float pressAngle = Element.PressAngle;
        float motion = Math.Max(0f, Element.HandMotion);

        for (int i = 0; i < HandsVisual.FingerCount; i++)
        {
            _angles[i] = _finger[i].Evaluate(now) * pressAngle;
            _press[i] = _finger[i].Evaluate(now);
        }

        var left = Pose(0, now, motion);
        var right = Pose(1, now, motion);
        Visual.Update(left, right, _angles, _press);
    }

    public override void ReleaseAll(long now)
    {
        for (int i = 0; i < HandsVisual.FingerCount; i++)
        {
            while (_held[i] > 0)
                Release(i, now);
        }

        _heldInputs.Clear();
    }

    public override void Reset()
    {
        _heldInputs.Clear();
        Array.Clear(_held, 0, _held.Length);
        Array.Clear(_heldInHand, 0, _heldInHand.Length);
        _groupFirst[0] = _groupFirst[1] = -1;
        for (int i = 0; i < _finger.Length; i++)
            _finger[i].Reset();
        for (int h = 0; h < 2; h++)
        {
            _zoom[h].Reset();
            _upDown[h].Reset();
            _leftRight[h].Reset();
            _xOffset[h].Reset();
        }
    }

    private void Press(int finger, long time)
    {
        _held[finger]++;
        var f = (Finger)finger;
        int h = (int)f.HandOf();
        _heldInHand[h]++;

        // 손가락: 끝까지 누름 (눌림 정도 0 → 1)
        _finger[finger].To(1f, time, Element.PressMs, Ease.Out);

        int number = OriginalNumber(f);
        float handMs = Element.HandPressMs;
        if (_heldInHand[h] == 1)
        {
            // 새 묶음: 손이 내려감
            _groupFirst[h] = number;
            _zoom[h].To(1f, time, handMs, Ease.Out);
            _upDown[h].To(1f, time, handMs, Ease.Out);
        }

        // 좌우 회전: 원작 GetDegree(지금 손가락, 묶음의 첫 손가락)
        float degree = Degree(number, _groupFirst[h]);
        _leftRight[h].To(degree, time, handMs, Ease.Out);
        _xOffset[h].To(XOffset(degree), time, handMs, Ease.Out);
    }

    private void Release(int finger, long time)
    {
        _held[finger]--;
        int h = (int)((Finger)finger).HandOf();
        _heldInHand[h]--;

        if (_held[finger] == 0)
            _finger[finger].After(0f, time, Element.ReleaseMs, Ease.Out);

        if (_heldInHand[h] == 0)
        {
            // 묶음 끝: 손이 원래 자세로
            float ms = Element.HandReleaseMs;
            _zoom[h].After(0f, time, ms, Ease.Out);
            _upDown[h].After(0f, time, ms, Ease.Out);
            _leftRight[h].After(0f, time, ms, Ease.InOut);
            _xOffset[h].After(0f, time, ms, Ease.InOut);
        }
    }

    private HandPose Pose(int h, long now, float motion)
    {
        // 원작은 왼손일 때 좌우 회전 부호를 반대로 함 (옆 이동은 렌더 위치에서 반대로 적용)
        float sign = h == (int)Hand.Left ? -1f : 1f;
        return new HandPose(
            1f + _zoom[h].Evaluate(now) * PressZoom * motion,
            _upDown[h].Evaluate(now) * PressUpDown * motion,
            _leftRight[h].Evaluate(now) * sign * motion * Math.Max(0f, Element.RollStrength),
            _xOffset[h].Evaluate(now) * motion);
    }

    /// <summary>원작 손가락 번호 (GetDegree의 i): 엄지 -1, 검지 0, 중지 1, 약지 2, 새끼 3.</summary>
    private static int OriginalNumber(Finger finger) => finger.IsThumb() ? -1 : 3 - finger.IndexInHand();

    /// <summary>원작 HandCam의 GetDegree: 바깥 손가락일수록 크게 돌고, 엄지로 시작하면 반대로 돔.</summary>
    private static float Degree(int current, int first)
    {
        if (current == -1)
            return -15f;
        return (float)((Math.Pow(2, current) - 1) * 3 - (Math.Pow(2, first) - 1));
    }

    /// <summary>원작: 손이 돈 만큼 옆으로 밀리는 거리.</summary>
    private static float XOffset(float degree) =>
        (float)((1 - Math.Cos(degree * Math.PI / 180)) * Basement * 4.0 * Math.Sign(degree));
}
