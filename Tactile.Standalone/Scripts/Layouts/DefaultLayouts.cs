using System.Linq;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Primitives;

namespace Tactile.Standalone.Layouts;

/// <summary>
/// 코드로 만든 기본 레이아웃. 레이아웃 파일 저장·불러오기와 에디터가 생기기 전까지 사용합니다.
/// </summary>
public static class DefaultLayouts
{
    private const float Margin = 20f;
    private const float KeySize = 80f;
    private const float KeyGap = 10f;
    private const float RainLength = 300f;
    private const float StatHeight = 36f;
    private const float HandsHeight = 220f;

    /// <summary>8키 + KPS / Total + 손 (왼손 새끼~검지: Tab 1 2 E, 오른손 검지~새끼: P = Backspace \).</summary>
    public static KeyViewerLayout FourKeys() => Keys(
        (KeyCode.Tab, Finger.LeftPinky),
        (KeyCode.D1, Finger.LeftRing),
        (KeyCode.D2, Finger.LeftMiddle),
        (KeyCode.E, Finger.LeftIndex),
        (KeyCode.PageDown, Finger.RightIndex),
        (KeyCode.Backslash, Finger.RightMiddle),
        (KeyCode.NumpadDivide, Finger.RightRing),
        (KeyCode.Numpad9, Finger.RightPinky));

    /// <summary>키를 가로로 나란히 놓고, 위로 레인, 아래에 KPS와 Total을 두는 레이아웃 (손 없음).</summary>
    public static KeyViewerLayout Keys(params KeyCode[] keys) => Keys(keys.Select(k => (k, (Finger?)null)).ToArray());

    /// <summary>
    /// 키를 가로로 나란히 놓고, 위로 레인, 아래에 KPS와 Total을 두는 레이아웃.
    /// 손가락이 지정된 키가 있으면 맨 아래에 손을 그립니다.
    /// </summary>
    public static KeyViewerLayout Keys(params (KeyCode Key, Finger? Finger)[] keys)
    {
        float width = Margin * 2 + keys.Length * KeySize + (keys.Length - 1) * KeyGap;
        float keyTop = Margin + RainLength;
        float statTop = keyTop + KeySize + KeyGap;
        bool hasHands = keys.Any(k => k.Finger is not null);
        float handsTop = statTop + StatHeight + KeyGap;
        float height = (hasHands ? handsTop + HandsHeight : statTop + StatHeight) + Margin;

        var layout = new KeyViewerLayout
        {
            Canvas = new Vector2(width, height),
        };

        for (int i = 0; i < keys.Length; i++)
        {
            KeyCode key = keys[i].Key;
            layout.Elements.Add(new KeyElement
            {
                Id = $"key-{key}",
                Bounds = new Rect(Margin + i * (KeySize + KeyGap), keyTop, KeySize, KeySize),
                Label = LabelOf(key),
                Style = CreateKeyStyle(), // 키마다 따로 가지므로 에디터에서 개별 수정 가능
                Rain = new RainSettings
                {
                    Enabled = true,
                    Direction = RainDirection.Up,
                    Speed = 900f,
                    MaxLength = RainLength,
                    Color = Color.FromHex("#7FD3FFC0"),
                },
                ShowCount = true,
                Bindings = { InputBinding.Keyboard(key) },
            });
        }

        float statWidth = (width - Margin * 2 - KeyGap) / 2f;
        layout.Elements.Add(new StatElement
        {
            Id = "kps",
            Bounds = new Rect(Margin, statTop, statWidth, StatHeight),
            Kind = StatKind.Kps,
            Format = "KPS {0:0}",
            Look = StatLook(),
        });
        layout.Elements.Add(new StatElement
        {
            Id = "total",
            Bounds = new Rect(Margin + statWidth + KeyGap, statTop, statWidth, StatHeight),
            Kind = StatKind.Total,
            Format = "Total {0}",
            Look = StatLook(),
        });

        if (hasHands)
        {
            var hands = new HandsElement
            {
                Id = "hands",
                Bounds = new Rect(Margin, handsTop, width - Margin * 2, HandsHeight),
                Background = new BoxStyle { Fill = Color.FromHex("#0B0D14E0"), CornerRadius = 10f },
                HandColor = Color.FromHex("#C4C8D0"),
                PressedColor = Color.FromHex("#9FDFFF"),
            };
            foreach ((KeyCode key, Finger? finger) in keys)
            {
                if (finger is not null)
                    hands.Fingers.Add(FingerBinding.Keyboard(key, finger.Value));
            }

            layout.Elements.Add(hands);
        }

        return layout;
    }

    private static KeyStyle CreateKeyStyle() => new()
    {
        Normal = new BoxStyle
        {
            Fill = Color.FromHex("#1E2230E0"),
            Border = Color.FromHex("#7FD3FF80"),
            TextColor = Color.FromHex("#E6EEF8"),
            BorderWidth = 2f,
            CornerRadius = 10f,
            FontSize = 26f,
        },
        Pressed = new BoxStyle
        {
            Fill = Color.FromHex("#7FD3FFF0"),
            Border = Color.FromHex("#FFFFFF"),
            TextColor = Color.FromHex("#10141E"),
            BorderWidth = 2f,
            CornerRadius = 10f,
            FontSize = 26f,
        },
        TransitionMs = 80f,
    };

    private static BoxStyle StatLook() => new()
    {
        Fill = Color.FromHex("#1E2230C0"),
        Border = Color.Transparent,
        TextColor = Color.FromHex("#E6EEF8"),
        CornerRadius = 8f,
        FontSize = 18f,
    };

    private static string LabelOf(KeyCode key) => key switch
    {
        >= KeyCode.D1 and <= KeyCode.D9 => ((int)(key - KeyCode.D1) + 1).ToString(),
        KeyCode.D0 => "0",
        KeyCode.Space => "Space",
        KeyCode.Backspace => "Back",
        KeyCode.Backslash => "\\",
        KeyCode.Equal => "=",
        KeyCode.Minus => "-",
        KeyCode.LeftShift or KeyCode.RightShift => "Shift",
        _ => key.ToString(),
    };
}
