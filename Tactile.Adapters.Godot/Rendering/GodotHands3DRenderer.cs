using Godot;
using Tactile.Adapters.Godot.Utils;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using CoreRect = Tactile.Core.Primitives.Rect;
using CoreVector3 = Tactile.Core.Primitives.Vector3;

namespace Tactile.Adapters.Godot.Rendering;

/// <summary>
/// (선택) 손 요소를 실제 3D로 그리는 렌더러. 기본 렌더러는 원작처럼 2D로 투영해 그리는 쪽이며,
/// 이 렌더러를 쓰려면 등록표에서 바꿔 등록하세요:
/// <c>renderers.Register&lt;HandsElement, HandsVisual&gt;((e, c) => new GodotHands3DRenderer(e, c));</c>
/// </summary>
/// <summary>
/// 손 요소를 3D로 그립니다. 전용 <see cref="SubViewport"/>(독립된 3D 월드) 안에서 Core가 계산한 손 뼈대 선분을
/// 캡슐로 그리고, 정사영 카메라로 찍은 결과를 요소 영역에 표시합니다.
/// 3D 화면은 모양이 바뀐 프레임에만 다시 렌더링합니다.
/// </summary>
public sealed class GodotHands3DRenderer : IElementRenderer<HandsVisual>
{
    private const float SuperSample = 2f;
    private const float Margin = 1.06f; // 손이 영역 가장자리에 닿지 않도록

    private readonly Control _node;
    private readonly BoxView _background;
    private readonly SubViewport _viewport;
    private readonly TextureRect _display;
    private readonly Camera3D _camera;
    private readonly Node3D[] _handRoots = new Node3D[2];
    private readonly MeshInstance3D[] _segments = new MeshInstance3D[HandsVisual.SegmentsPerHand * 2];
    private readonly StandardMaterial3D[] _materials = new StandardMaterial3D[HandsVisual.SegmentsPerHand * 2];
    private readonly Tactile.Core.Primitives.Color[] _colors = new Tactile.Core.Primitives.Color[HandsVisual.SegmentsPerHand * 2];
    private float _spacing;
    private int _appliedRevision = -1;
    private Vector2I _viewportSize;

    public GodotHands3DRenderer(HandsElement element, GodotRenderContext context)
    {
        _node = GodotRenderContext.CreateContainer($"Hands_{element.Id}");
        context.ElementLayer.AddChild(_node);
        _background = new BoxView(_node, context.TextureResolver);

        // 게임의 3D 월드와 섞이지 않도록 자체 월드 사용
        _viewport = new SubViewport
        {
            Name = "Viewport3D",
            OwnWorld3D = true,
            TransparentBg = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        _node.AddChild(_viewport);

        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Current = true,
            Near = 0.05f,
            Far = 40f,
            Position = new Vector3(0f, 0f, 20f),
            Environment = CreateEnvironment(),
        };
        _viewport.AddChild(_camera);
        // 위 왼쪽 앞에서 비추는 주 조명 + 반대쪽 약한 보조 조명
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35f, -30f, 0f), LightEnergy = 0.9f });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(20f, 150f, 0f), LightEnergy = 0.3f });

        var capsule = new CapsuleMesh { Radius = 0.5f, Height = 2f, RadialSegments = 20, Rings = 6 };
        for (int h = 0; h < 2; h++)
        {
            _handRoots[h] = new Node3D { Name = $"{(Hand)h}Hand" };
            _viewport.AddChild(_handRoots[h]);
            for (int i = 0; i < HandsVisual.SegmentsPerHand; i++)
            {
                int k = h * HandsVisual.SegmentsPerHand + i;
                _materials[k] = new StandardMaterial3D { Roughness = 0.6f, Metallic = 0f };
                _segments[k] = new MeshInstance3D { Mesh = capsule, MaterialOverride = _materials[k] };
                _handRoots[h].AddChild(_segments[k]);
            }
        }

        _display = new TextureRect
        {
            Name = "Display",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = _viewport.GetTexture(),
        };
        _node.AddChild(_display);
    }

    public void Render(HandsVisual visual)
    {
        _background.Apply(visual.Bounds, visual.Background);

        CoreRect bounds = visual.Bounds;
        _display.Position = new Vector2(bounds.X, bounds.Y);
        _display.Size = new Vector2(bounds.Width, bounds.Height);

        var size = new Vector2I(
            Math.Max(1, (int)Math.Round(bounds.Width * SuperSample)),
            Math.Max(1, (int)Math.Round(bounds.Height * SuperSample)));

        bool layoutChanged = size != _viewportSize;
        if (layoutChanged)
        {
            _viewportSize = size;
            _viewport.Size = size;
            PlaceCamera(visual, (float)size.X / size.Y);
        }

        if (!layoutChanged && visual.Revision == _appliedRevision)
            return;
        _appliedRevision = visual.Revision;

        for (int h = 0; h < 2; h++)
        {
            var hand = (Hand)h;
            float side = hand == Hand.Left ? -1f : 1f;
            _handRoots[h].Position = new Vector3(side * _spacing + visual.GetOffset(hand), 0f, 0f);

            for (int i = 0; i < HandsVisual.SegmentsPerHand; i++)
            {
                int k = h * HandsVisual.SegmentsPerHand + i;
                HandSegment segment = visual.GetSegment(hand, i);
                _segments[k].Transform = CapsuleTransform(segment);
                if (segment.Color != _colors[k] || layoutChanged)
                {
                    _colors[k] = segment.Color;
                    _materials[k].AlbedoColor = segment.Color.ToGodot();
                }
            }
        }

        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    public void Dispose()
    {
        _background.Dispose();
        if (GodotObject.IsInstanceValid(_node))
            _node.QueueFree();
    }

    /// <summary>
    /// 쉬는 자세의 두 손이 영역에 꽉 차도록 정사영 범위와 두 손 간격을 정합니다.
    /// 영역이 가로로 넓으면 손 사이를 벌리고, 좁으면 범위를 넓혀 두 손이 다 들어오게 합니다.
    /// </summary>
    private void PlaceCamera(HandsVisual visual, float aspect)
    {
        CoreRect right = visual.GetRestBounds(Hand.Right);
        float halfHeight = right.Height / 2f * Margin;
        float centerY = right.Y + right.Height / 2f;

        // 오른손 영역의 왼쪽 끝이 가운데(0)에서 조금 떨어지도록: 손 사이 간격은 손 너비의 15%
        float gap = right.Width * 0.15f;
        float minSpacing = gap / 2f - right.X;
        float halfWidthNeeded = (minSpacing + right.X + right.Width) * Margin;

        // 영역이 넓으면 남는 가로 공간만큼 손을 바깥쪽으로 (각 손이 화면 절반의 가운데쯤)
        float visibleHalfWidth = halfHeight * aspect;
        if (halfWidthNeeded > visibleHalfWidth)
        {
            halfHeight = halfWidthNeeded / aspect;
            visibleHalfWidth = halfWidthNeeded;
        }

        float handCenter = right.X + right.Width / 2f;
        _spacing = Math.Max(minSpacing, visibleHalfWidth / 2f - handCenter);

        _camera.Size = halfHeight * 2f;
        _camera.Position = new Vector3(0f, centerY, 20f);
    }

    private static Transform3D CapsuleTransform(in HandSegment segment)
    {
        Vector3 a = ToGodot(segment.From), b = ToGodot(segment.To);
        Vector3 axis = b - a;
        float length = axis.Length();
        float r = segment.Radius;
        Vector3 y = length > 1e-5f ? axis / length : Vector3.Up;

        // 축(y)에 수직인 두 방향
        Vector3 helper = Math.Abs(y.Dot(Vector3.Forward)) < 0.9f ? Vector3.Forward : Vector3.Right;
        Vector3 x = y.Cross(helper).Normalized();
        Vector3 z = x.Cross(y);

        // 기본 캡슐: 반지름 0.5, 전체 높이 2 → 반지름 r, 전체 높이 length + 2r
        var basis = new Basis(x * (r * 2f), y * ((length + r * 2f) / 2f), z * (r * 2f));
        return new Transform3D(basis, (a + b) / 2f);
    }

    private static Vector3 ToGodot(CoreVector3 v) => new(v.X, v.Y, v.Z);

    private static global::Godot.Environment CreateEnvironment() => new()
    {
        BackgroundMode = global::Godot.Environment.BGMode.ClearColor,
        AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
        AmbientLightColor = Colors.White,
        AmbientLightEnergy = 0.35f,
        TonemapMode = global::Godot.Environment.ToneMapper.Filmic,
    };
}
