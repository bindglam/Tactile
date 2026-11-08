using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Primitives;

namespace Tactile.Core.Rendering;

/// <summary>
/// 손 뼈대의 선분 하나. 렌더러는 <see cref="From"/>에서 <see cref="To"/>까지 반지름 <see cref="Radius"/>인
/// 원기둥(양 끝이 둥근 캡슐)을 그리면 됩니다.
/// <para>
/// 좌표는 손마다 손목이 원점인 화면 공간입니다: X는 오른쪽, Y는 위, Z는 보는 사람 쪽(앞)이 양수.
/// 단위는 손바닥 너비가 약 1입니다. 정사영(원근 없음)으로 보는 것을 기준으로 합니다.
/// </para>
/// </summary>
public readonly record struct HandSegment(Vector3 From, Vector3 To, float Radius, Color Color);

/// <summary>
/// 화면에 그릴 굵은 선 하나 (레이아웃 픽셀 좌표). 양 끝이 둥근 선으로 그리면 됩니다.
/// <see cref="HandsVisual.GetStroke"/>는 먼 것부터 순서대로 주므로, 순서대로 덮어 그리면 앞뒤가 맞습니다.
/// </summary>
public readonly record struct HandStroke(Vector2 From, Vector2 To, float Width, Color Color);

/// <summary>한 손의 자세 (원작 손 캠의 손 단위 애니메이션 값).</summary>
internal readonly record struct HandPose(float Zoom, float UpDown, float LeftRight, float XOffset);

/// <summary>
/// 두 손을 3D로 그리는 데 필요한 값. 손 뼈대 계산은 ADOFAI Macro 3(FFLiK, MIT — THIRD_PARTY_NOTICES.md 참고)의 <c>Hand::Simulate</c>를 옮긴 것입니다.
/// <see cref="Revision"/>이 바뀌었을 때만 다시 그리면 됩니다.
/// </summary>
public sealed class HandsVisual
{
    public const int FingerCount = 10;

    /// <summary>손 하나의 선분 수: 손바닥 4 + 손가락 4개 × 5 + 엄지 4.</summary>
    public const int SegmentsPerHand = 28;

    // 원작 단위(픽셀) → 출력 단위
    private const float Unit = 1f / 100f;

    // 원작 손 치수 (Hand::Length), 손 안 순서 0 = 새끼 ~ 3 = 검지, 4 = 엄지
    private static readonly float[] HandX = [50f, 25f, 0f, -25f, -50f];       // 손목 기준 손가락 뿌리 위치 (오른손)
    private static readonly float[] BaseLength = [130f, 150f, 160f, 160f, 110f];
    private static readonly float[] BeginLength = [70f, 85f, 90f, 85f, 70f];
    private static readonly float[] MiddleLength = [40f, 60f, 60f, 60f, 45f];
    private static readonly float[] EndLength = [30f, 30f, 40f, 40f, 0f];
    private static readonly float[] Direction = [70f, 80f, 90f, 100f, 125f]; // 손가락이 뻗는 방향(도)
    private static readonly float[] WidthOffset = [-10f, 0f, 0f, 0f, 10f];   // 새끼는 가늘게, 엄지는 굵게

    // 원작 쉬는 자세 (HandCam::Init)
    private const float RestBegin = 20f, RestMiddle = 45f, RestEnd = 20f;
    private const float ThumbRoll = 50f;
    private const float HandWidth = 50f;

    private readonly HandsElement _element;
    private readonly HandSegment[] _segments = new HandSegment[SegmentsPerHand * 2];
    private readonly float[] _offsets = new float[2];
    private readonly Rect[] _restBounds = new Rect[2];

    // 2D 투영 (원작처럼 정사영으로 화면에 바로 그림)
    private const float FitMargin = 1.06f;  // 손이 영역 가장자리에 닿지 않도록
    private const float HandGap = 0.15f;    // 두 손 사이 최소 간격 (손 너비 대비)
    private const float DepthShade = 0.22f; // 1 단위 멀어질 때 어두워지는 정도
    private readonly HandStroke[] _strokes = new HandStroke[SegmentsPerHand * 2];
    private readonly int[] _order = new int[SegmentsPerHand * 2];
    private readonly float[] _depth = new float[SegmentsPerHand * 2];
    private float _pixelsPerUnit, _spacing, _centerY;

    // 계산용 (할당 없이 재사용)
    private readonly V[] _hand = new V[5], _basement = new V[5], _begin = new V[5], _middle = new V[5], _end = new V[5];

    internal HandsVisual(HandsElement element)
    {
        _element = element;
        Bounds = element.Bounds;
        Background = element.Background;
        HandColor = element.HandColor;

        var rest = new HandPose(1f, 0f, 0f, 0f);
        var angles = new float[FingerCount];
        for (int h = 0; h < 2; h++)
        {
            Build((Hand)h, rest, angles, angles);
            _restBounds[h] = MeasureBounds((Hand)h);
        }

        Fit();
        Update(rest, rest, angles, angles, force: true);
    }

    public HandsElement Element => _element;

    /// <summary>손을 그릴 영역.</summary>
    public Rect Bounds { get; }

    public BoxStyle Background { get; }

    public Color HandColor { get; }

    /// <summary>모양이 바뀔 때마다 1씩 늘어납니다.</summary>
    public int Revision { get; private set; }

    /// <summary>손 뼈대의 선분 (<paramref name="index"/> = 0 ~ <see cref="SegmentsPerHand"/> - 1). 손목 기준 좌표.</summary>
    public HandSegment GetSegment(Hand hand, int index) => _segments[(int)hand * SegmentsPerHand + index];

    /// <summary>손이 옆으로 밀린 거리 (화면 오른쪽이 양수). 손을 놓는 위치에 더합니다.</summary>
    public float GetOffset(Hand hand) => _offsets[(int)hand];

    /// <summary>쉬는 자세에서 손이 차지하는 영역 (손목 기준, Y는 위가 양수). 카메라 범위를 정할 때 사용합니다.</summary>
    public Rect GetRestBounds(Hand hand) => _restBounds[(int)hand];

    /// <summary>2D로 그릴 선 수 (<see cref="GetStroke"/>).</summary>
    public int StrokeCount => _strokes.Length;

    /// <summary>
    /// 2D로 그릴 선 (레이아웃 픽셀 좌표, 먼 것부터). 원작처럼 뼈대를 정사영으로 화면에 투영하고, 깊이에 따라 조금 어둡게 칠한 것입니다.
    /// </summary>
    public HandStroke GetStroke(int index) => _strokes[_order[index]];

    /// <summary>쉬는 두 손이 영역에 꽉 차도록 할 때, 가운데에서 각 손목까지의 거리 (3D 단위).</summary>
    public float HandSpacing => _spacing;

    /// <summary>3D 단위 1이 화면에서 몇 픽셀인지.</summary>
    public float PixelsPerUnit => _pixelsPerUnit;

    /// <summary>영역 세로 가운데에 오는 3D 높이 (손목 기준 Y).</summary>
    public float CenterY => _centerY;

    /// <param name="angles">손가락별 뿌리 마디 추가 각도 (도, <see cref="Finger"/> 순서)</param>
    /// <param name="press">손가락별 눌림 정도 (0 ~ 1, 색에 사용)</param>
    internal void Update(in HandPose left, in HandPose right, float[] angles, float[] press, bool force = false)
    {
        bool changed = force;
        for (int h = 0; h < 2; h++)
        {
            int start = h * SegmentsPerHand;
            changed |= Build((Hand)h, h == 0 ? left : right, angles, press);

            float offset = (h == 0 ? left : right).XOffset * Unit * (h == 0 ? -1f : 1f);
            if (offset != _offsets[h])
            {
                _offsets[h] = offset;
                changed = true;
            }
        }

        if (changed)
        {
            Project();
            Revision++;
        }
    }

    /// <summary>
    /// 쉬는 자세의 두 손이 영역에 꽉 차도록 배율과 두 손 간격을 정합니다.
    /// 영역이 가로로 넓으면 손 사이를 벌리고, 좁으면 배율을 줄여 두 손이 다 들어오게 합니다.
    /// </summary>
    private void Fit()
    {
        Rect right = _restBounds[(int)Hand.Right];
        float aspect = Bounds.Height > 0f ? Bounds.Width / Bounds.Height : 1f;
        float halfHeight = right.Height / 2f * FitMargin;
        _centerY = right.Y + right.Height / 2f;

        float minSpacing = right.Width * HandGap / 2f - right.X;
        float halfWidthNeeded = (minSpacing + right.X + right.Width) * FitMargin;
        float visibleHalfWidth = halfHeight * aspect;
        if (halfWidthNeeded > visibleHalfWidth)
        {
            halfHeight = halfWidthNeeded / aspect;
            visibleHalfWidth = halfWidthNeeded;
        }

        float handCenter = right.X + right.Width / 2f;
        _spacing = Math.Max(minSpacing, visibleHalfWidth / 2f - handCenter);
        _pixelsPerUnit = halfHeight > 0f ? Bounds.Height / 2f / halfHeight : 1f;
    }

    /// <summary>뼈대 선분을 화면 선으로 투영하고 먼 것부터 정렬합니다.</summary>
    private void Project()
    {
        float cx = Bounds.X + Bounds.Width / 2f;
        float cy = Bounds.Y + Bounds.Height / 2f;
        float k = _pixelsPerUnit;

        for (int i = 0; i < _segments.Length; i++)
        {
            int h = i / SegmentsPerHand;
            HandSegment seg = _segments[i];
            float shiftX = (h == (int)Hand.Left ? -_spacing : _spacing) + _offsets[h];
            var from = new Vector2(cx + (seg.From.X + shiftX) * k, cy - (seg.From.Y - _centerY) * k);
            var to = new Vector2(cx + (seg.To.X + shiftX) * k, cy - (seg.To.Y - _centerY) * k);

            // 깊이: 선분 가운데 (Z는 보는 사람 쪽이 양수)
            float depth = (seg.From.Z + seg.To.Z) / 2f;
            float shade = Math.Max(0.55f, Math.Min(1.1f, 1f + depth * DepthShade));
            Color c = seg.Color;
            var color = new Color(Math.Min(1f, c.R * shade), Math.Min(1f, c.G * shade), Math.Min(1f, c.B * shade), c.A);

            _strokes[i] = new HandStroke(from, to, seg.Radius * 2f * k, color);
            _depth[i] = depth;
            _order[i] = i;
        }

        // 먼 것(Z가 작은 것)부터. 삽입 정렬 (할당 없음)
        for (int i = 1; i < _order.Length; i++)
        {
            int item = _order[i];
            float d = _depth[item];
            int j = i - 1;
            while (j >= 0 && _depth[_order[j]] > d)
            {
                _order[j + 1] = _order[j];
                j--;
            }

            _order[j + 1] = item;
        }
    }

    /// <summary>원작 Hand::Simulate. 선분이 바뀌었으면 true.</summary>
    private bool Build(Hand hand, in HandPose pose, float[] angles, float[] press)
    {
        bool left = hand == Hand.Left;
        float zoom = pose.Zoom;

        // 원작 기본 자세: 오른손 degree_factor -10, leftright +8 / 왼손은 반대. updown = Tilt
        float roll = _element.Tilt + pose.UpDown;
        float pitch = (left ? -8f : 8f) + pose.LeftRight;
        float yaw = left ? 10f : -10f;

        for (int i = 0; i < 5; i++)
        {
            var finger = Fingers.Of(hand, i);
            float extra = angles[(int)finger];
            bool thumb = i == 4;

            float baseLength = BaseLength[i] * zoom;
            float l1 = BeginLength[i] * zoom, l2 = MiddleLength[i] * zoom, l3 = EndLength[i] * zoom;

            // CalculateFingerAngle: 손가락은 x축 방향으로 뻗고, 각도만큼 +z 쪽으로 굽음
            float a1, a2, a3;
            if (thumb)
            {
                a1 = RestBegin; a2 = RestBegin; a3 = 0f; // thumb_middle, thumb_end, 0
            }
            else
            {
                a1 = RestBegin + extra; a2 = RestMiddle; a3 = RestEnd;
            }

            var b0 = new V(baseLength, 0f, 0f);
            double angle = Rad(a1);
            var b1 = b0 + new V(l1 * Math.Cos(angle), 0, l1 * Math.Sin(angle));
            angle += Rad(a2);
            var b2 = b1 + new V(l2 * Math.Cos(angle), 0, l2 * Math.Sin(angle));
            angle += Rad(a3);
            var b3 = b2 + new V(l3 * Math.Cos(angle), 0, l3 * Math.Sin(angle));

            // CalculateThumbBasementAngle: 엄지는 뿌리째 기울어 누름 (thumb_base)
            if (thumb)
            {
                double r = Rad(ThumbRoll), p = Rad(RestBegin + extra);
                b0 = b0.Rotate(r, p, 0); b1 = b1.Rotate(r, p, 0); b2 = b2.Rotate(r, p, 0); b3 = b3.Rotate(r, p, 0);
            }

            // SetFingerDirection
            double dir = Rad(Direction[i]);
            b0 = b0.Rotate(0, 0, dir); b1 = b1.Rotate(0, 0, dir); b2 = b2.Rotate(0, 0, dir); b3 = b3.Rotate(0, 0, dir);
            var root = new V(HandX[i] * zoom, 0f, 0f);

            // LeftRightInversion (왼손), UpDownInversion
            if (left)
            {
                root = root.FlipX(); b0 = b0.FlipX(); b1 = b1.FlipX(); b2 = b2.FlipX(); b3 = b3.FlipX();
            }

            root = root.FlipY(); b0 = b0.FlipY(); b1 = b1.FlipY(); b2 = b2.FlipY(); b3 = b3.FlipY();

            // RotateCameraView
            double cr = Rad(roll), cp = Rad(pitch), cy = Rad(yaw);
            _hand[i] = root.Rotate(cr, cp, cy);
            _basement[i] = (b0 + root).Rotate(cr, cp, cy);
            _begin[i] = (b1 + root).Rotate(cr, cp, cy);
            _middle[i] = (b2 + root).Rotate(cr, cp, cy);
            _end[i] = (b3 + root).Rotate(cr, cp, cy);
        }

        // AdvancedDraw_Depth의 선분과 굵기
        int s = (int)hand * SegmentsPerHand;
        bool changed = false;
        float width = HandWidth * zoom;
        Color handColor = HandColor;

        // 손바닥: 엄지 → 검지 → 중지 → 약지 → 새끼 뿌리
        for (int k = 0; k < 4; k++)
            changed |= Set(s++, _basement[4 - k], _basement[3 - k], width, handColor);

        for (int i = 0; i < 5; i++)
        {
            var finger = Fingers.Of(hand, i);
            Color color = Color.Lerp(handColor, _element.PressedColor, Math.Max(0f, Math.Min(1f, press[(int)finger])));
            float w = width + WidthOffset[i];
            bool thumb = i == 4;

            changed |= Set(s++, V.Zero, _hand[i], w, handColor);
            changed |= Set(s++, _hand[i], _basement[i], w, thumb ? color : handColor);
            changed |= Set(s++, _basement[i], _begin[i], w, color);
            changed |= Set(s++, _begin[i], _middle[i], w, color);
            if (!thumb)
                changed |= Set(s++, _middle[i], _end[i], w, color);
        }

        return changed;
    }

    private bool Set(int index, V from, V to, float width, Color color)
    {
        var segment = new HandSegment(from.ToView(), to.ToView(), width / 2f * Unit, color);
        if (segment == _segments[index])
            return false;
        _segments[index] = segment;
        return true;
    }

    private Rect MeasureBounds(Hand hand)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < SegmentsPerHand; i++)
        {
            HandSegment seg = GetSegment(hand, i);
            foreach (Vector3 p in new[] { seg.From, seg.To })
            {
                minX = Math.Min(minX, p.X - seg.Radius);
                maxX = Math.Max(maxX, p.X + seg.Radius);
                minY = Math.Min(minY, p.Y - seg.Radius);
                maxY = Math.Max(maxY, p.Y + seg.Radius);
            }
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>원작 좌표 (x 오른쪽, y 위, 단위 픽셀). 계산은 double로.</summary>
    private readonly struct V(double x, double y, double z)
    {
        public readonly double X = x, Y = y, Z = z;

        public static V Zero => default;

        public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public V FlipX() => new(-X, Y, Z);

        public V FlipY() => new(X, -Y, Z);

        // 원작은 2D로 투영만 하고 z는 그리는 순서에만 씀. 손가락이 +z로 굽으므로, 손등 쪽에서 내려다보도록
        // (오른손 엄지가 화면 왼쪽) +z를 보는 사람에게서 먼 쪽으로 둠
        public Vector3 ToView() => new((float)(X * Unit), (float)(Y * Unit), (float)(-Z * Unit));

        /// <summary>
        /// 원작 Vector::Rotate: 행벡터 v에 R = Rx(roll)·Ry(pitch)·Rz(yaw)를 오른쪽에서 곱함 (vᵀR).
        /// </summary>
        public V Rotate(double roll, double pitch, double yaw)
        {
            double cr = Math.Cos(roll), sr = Math.Sin(roll);
            double cp = Math.Cos(pitch), sp = Math.Sin(pitch);
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);

            // Rx·Ry
            double m00 = cp, m01 = 0, m02 = sp;
            double m10 = sr * sp, m11 = cr, m12 = -sr * cp;
            double m20 = -cr * sp, m21 = sr, m22 = cr * cp;

            // (Rx·Ry)·Rz
            double r00 = m00 * cy + m01 * sy, r01 = -m00 * sy + m01 * cy, r02 = m02;
            double r10 = m10 * cy + m11 * sy, r11 = -m10 * sy + m11 * cy, r12 = m12;
            double r20 = m20 * cy + m21 * sy, r21 = -m20 * sy + m21 * cy, r22 = m22;

            return new V(
                X * r00 + Y * r10 + Z * r20,
                X * r01 + Y * r11 + Z * r21,
                X * r02 + Y * r12 + Z * r22);
        }
    }
}
