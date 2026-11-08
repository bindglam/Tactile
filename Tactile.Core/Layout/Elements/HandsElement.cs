using Tactile.Core.Input.Keyboard;
using Tactile.Core.Primitives;

namespace Tactile.Core.Layout.Elements;

/// <summary>
/// 키보드 위에 올린 두 손. 움직임과 손 모양은 ADOFAI Macro 3(FFLiK, MIT — THIRD_PARTY_NOTICES.md 참고)의 손 캠을 바탕으로 합니다:
/// <list type="bullet">
/// <item>손가락: 누르면 뿌리 마디가 <see cref="PressAngle"/>만큼 내려감</item>
/// <item>손 (주된 움직임): 한 손의 연타(겹쳐 누르기 포함)가 시작되면 손이 내려가며(작아지고 앞으로 숙임),
/// 누르는 손가락 패턴에 따라 좌우로 돌고 옆으로 밀림. 모두 떼면 돌아옴</item>
/// </list>
/// <para>
/// 원작은 매크로라 앞으로 칠 입력을 미리 알고 누르기 전에 손가락을 들어 올리는 준비 동작을 하지만,
/// 키뷰어는 미래 입력을 모르므로 모든 동작이 누른 순간부터 시작합니다.
/// Core는 손 뼈대와 그것을 원작처럼 2D로 투영한 굵은 선까지 계산하고, 엔진 렌더러는 그리기만 합니다.
/// </para>
/// </summary>
public sealed class HandsElement : LayoutElement
{
    /// <summary>입력 → 손가락 연결. 한 손가락에 여러 키를 연결할 수 있습니다.</summary>
    public List<FingerBinding> Fingers { get; set; } = new();

    /// <summary>손 영역 배경. 기본은 투명입니다.</summary>
    public BoxStyle Background { get; set; } = new() { Fill = Color.Transparent, Border = Color.Transparent };

    /// <summary>손 색. 원작은 회색(HSV 0, 0, 50%).</summary>
    public Color HandColor { get; set; } = Color.FromRgba(128, 128, 128);

    /// <summary>눌린 손가락 색. 손 색과 같으면 색은 바뀌지 않습니다.</summary>
    public Color PressedColor { get; set; } = Color.White;

    /// <summary>누를 때 손가락 뿌리 마디가 내려가는 각도(도). 0이면 손가락은 움직이지 않습니다.</summary>
    public float PressAngle { get; set; } = 15f;

    /// <summary>손 전체 움직임(내려감, 숙임, 좌우 회전, 옆 이동)의 크기. 1이 원작과 같고, 0이면 손은 고정됩니다.</summary>
    public float HandMotion { get; set; } = 1f;

    /// <summary>손이 좌우로 기우는(돌아가는) 정도. 원작 각도에 곱하며, 1이 원작과 같습니다. 옆으로 밀리는 거리는 바뀌지 않습니다.</summary>
    public float RollStrength { get; set; } = 1f;

    /// <summary>손가락이 내려가는 시간(ms).</summary>
    public float PressMs { get; set; } = 50f;

    /// <summary>손가락이 다시 올라오는 시간(ms). 원작 50ms.</summary>
    public float ReleaseMs { get; set; } = 50f;

    /// <summary>손이 내려가고 도는 시간(ms).</summary>
    public float HandPressMs { get; set; } = 50f;

    /// <summary>모두 뗀 뒤 손이 원래 자세로 돌아오는 시간(ms).</summary>
    public float HandReleaseMs { get; set; } = 120f;

    /// <summary>손을 앞으로 기울여 보는 각도(도). 원작 30.</summary>
    public float Tilt { get; set; } = 30f;
}

/// <summary>손.</summary>
public enum Hand
{
    Left,
    Right,
}

/// <summary>손가락. 화면 왼쪽부터 오른쪽 순서입니다 (손바닥을 아래로 둔 모양).</summary>
public enum Finger
{
    LeftPinky,
    LeftRing,
    LeftMiddle,
    LeftIndex,
    LeftThumb,
    RightThumb,
    RightIndex,
    RightMiddle,
    RightRing,
    RightPinky,
}

/// <summary>입력 하나를 손가락 하나에 연결합니다.</summary>
public readonly record struct FingerBinding(InputBinding Input, Finger Finger)
{
    public static FingerBinding Keyboard(KeyCode key, Finger finger) => new(InputBinding.Keyboard(key), finger);
}

/// <summary>손가락 관련 도우미.</summary>
public static class Fingers
{
    /// <summary>손가락이 속한 손.</summary>
    public static Hand HandOf(this Finger finger) => finger <= Finger.LeftThumb ? Hand.Left : Hand.Right;

    /// <summary>엄지인지.</summary>
    public static bool IsThumb(this Finger finger) => finger is Finger.LeftThumb or Finger.RightThumb;

    /// <summary>손 안에서의 순서: 0 = 새끼, 1 = 약지, 2 = 중지, 3 = 검지, 4 = 엄지.</summary>
    public static int IndexInHand(this Finger finger) =>
        finger <= Finger.LeftThumb ? (int)finger : Finger.RightPinky - finger;

    /// <summary>손과 손 안의 순서(0 = 새끼 ~ 4 = 엄지)로 손가락을 찾습니다.</summary>
    public static Finger Of(Hand hand, int indexInHand) =>
        hand == Hand.Left ? (Finger)indexInHand : (Finger)((int)Finger.RightPinky - indexInHand);
}
