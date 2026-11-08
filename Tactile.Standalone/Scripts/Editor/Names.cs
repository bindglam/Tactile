using System;
using System.Collections.Generic;
using Tactile.Core.Layout.Elements;

namespace Tactile.Standalone.Editor;

/// <summary>에디터에 보이는 이름. 없으면 코드 이름을 그대로 씁니다 (애드온 요소 등).</summary>
public static class Names
{
    private static readonly Dictionary<string, string> Properties = new()
    {
        ["Id"] = "ID",
        ["Bounds"] = "위치·크기",
        ["Canvas"] = "캔버스 크기",
        ["Scale"] = "배율",
        ["Label"] = "글자",
        ["Bindings"] = "입력",
        ["Style"] = "모양",
        ["Normal"] = "평소",
        ["Pressed"] = "눌림",
        ["TransitionMs"] = "전환 시간 (ms)",
        ["Rain"] = "레인",
        ["Enabled"] = "사용",
        ["Direction"] = "방향",
        ["Speed"] = "속도 (px/s)",
        ["MaxLength"] = "최대 길이",
        ["Color"] = "색",
        ["Width"] = "너비 (0: 키에 맞춤)",
        ["Offset"] = "오프셋",
        ["ShowCount"] = "누른 횟수 표시",
        ["Kind"] = "종류",
        ["Look"] = "모양",
        ["Format"] = "표시 형식",
        ["Fill"] = "채우기",
        ["Border"] = "테두리",
        ["TextColor"] = "글자 색",
        ["BorderWidth"] = "테두리 두께",
        ["CornerRadius"] = "모서리 둥글기",
        ["FontSize"] = "글자 크기",
        ["SpriteKey"] = "이미지",
        ["Fingers"] = "손가락 연결",
        ["Background"] = "배경",
        ["HandColor"] = "손 색",
        ["PressedColor"] = "눌림 색",
        ["PressAngle"] = "손가락 각도",
        ["HandMotion"] = "손 움직임",
        ["RollStrength"] = "기울임 강도",
        ["PressMs"] = "누르는 시간 (ms)",
        ["ReleaseMs"] = "떼는 시간 (ms)",
        ["HandPressMs"] = "손 누르는 시간 (ms)",
        ["HandReleaseMs"] = "손 돌아오는 시간 (ms)",
        ["Tilt"] = "보는 각도",
        ["Input"] = "입력",
        ["Finger"] = "손가락",
    };

    private static readonly Dictionary<Enum, string> EnumValues = new()
    {
        [Finger.LeftPinky] = "왼손 새끼",
        [Finger.LeftRing] = "왼손 약지",
        [Finger.LeftMiddle] = "왼손 중지",
        [Finger.LeftIndex] = "왼손 검지",
        [Finger.LeftThumb] = "왼손 엄지",
        [Finger.RightThumb] = "오른손 엄지",
        [Finger.RightIndex] = "오른손 검지",
        [Finger.RightMiddle] = "오른손 중지",
        [Finger.RightRing] = "오른손 약지",
        [Finger.RightPinky] = "오른손 새끼",
        [RainDirection.Up] = "위",
        [RainDirection.Down] = "아래",
        [RainDirection.Left] = "왼쪽",
        [RainDirection.Right] = "오른쪽",
        [StatKind.Kps] = "KPS",
        [StatKind.MaxKps] = "최대 KPS",
        [StatKind.Total] = "총 횟수",
    };

    public static string Property(string name) => Properties.TryGetValue(name, out string? text) ? text : name;

    public static string EnumValue(Enum value) => EnumValues.TryGetValue(value, out string? text) ? text : value.ToString();

    /// <summary>요소 종류 이름 (목록, 추가 메뉴).</summary>
    public static string ElementKind(Type type) => type.Name switch
    {
        nameof(KeyElement) => "키",
        nameof(StatElement) => "통계",
        nameof(HandsElement) => "손",
        nameof(UnknownElement) => "알 수 없음",
        _ => type.Name.EndsWith("Element", StringComparison.Ordinal) ? type.Name.Substring(0, type.Name.Length - 7) : type.Name,
    };

    /// <summary>목록에 보일 요소 설명.</summary>
    public static string Describe(LayoutElement element)
    {
        string detail = element switch
        {
            KeyElement key when key.Label.Length > 0 => key.Label,
            StatElement stat => EnumValue(stat.Kind),
            UnknownElement unknown => unknown.TypeId,
            _ => element.Id,
        };
        return $"{ElementKind(element.GetType())} · {detail}";
    }
}
