using Tactile.Adapters.Unity.Utils;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Rendering;
using UnityEngine;
using CoreColor = Tactile.Core.Primitives.Color;
using CoreRect = Tactile.Core.Primitives.Rect;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>
/// (선택) 손 요소를 실제 3D로 그리는 렌더러. 기본 렌더러는 원작처럼 2D로 투영해 그리는 쪽이며,
/// 이 렌더러를 쓰려면 등록표에서 바꿔 등록하세요:
/// <c>renderers.Register&lt;HandsElement, HandsVisual&gt;((e, c) => new UnityHands3DRenderer(e, c));</c>
/// </summary>
/// <summary>
/// 손 요소를 3D로 그립니다. 게임 화면에서 멀리 떨어진 곳에 캡슐들과 전용 정사영 카메라를 두고,
/// Core가 계산한 손 뼈대 선분대로 캡슐을 배치해 <see cref="RenderTexture"/>에 렌더링한 뒤 요소 영역에 표시합니다.
/// <para>
/// Core 좌표는 Z가 보는 사람 쪽이 양수이고, Unity 카메라는 +Z를 바라보므로 z 부호만 바꿉니다.
/// 카메라는 꺼 두고 모양이 바뀐 프레임에만 직접 <see cref="Camera.Render"/>를 호출합니다.
/// </para>
/// </summary>
public sealed class UnityHands3DRenderer : IElementRenderer<HandsVisual>
{
    private const float SuperSample = 2f;
    private const float Margin = 1.06f;

    // 게임 장면과 겹치지 않도록 손 모델을 두는 위치
    private static readonly Vector3 SceneOrigin = new(0f, -20000f, 0f);
    private static int s_sceneCount;
    private static Mesh? s_capsule;

    private readonly RectTransform _node;
    private readonly BoxView _background;
    private readonly MeshView _display;
    private readonly GameObject _scene;
    private readonly Camera _camera;
    private readonly Transform[] _handRoots = new Transform[2];
    private readonly Transform[] _segments = new Transform[HandsVisual.SegmentsPerHand * 2];
    private readonly Material[] _materials = new Material[HandsVisual.SegmentsPerHand * 2];
    private readonly CoreColor[] _colors = new CoreColor[HandsVisual.SegmentsPerHand * 2];
    private RenderTexture? _texture;
    private float _spacing;
    private int _appliedRevision = -1;
    private Vector2Int _textureSize;

    public UnityHands3DRenderer(HandsElement element, UnityRenderContext context)
    {
        _node = UnityRenderContext.CreateNode($"Hands_{element.Id}", context.ElementLayer);
        _background = new BoxView(_node, context.TextureResolver);
        _display = new MeshView("Display", _node);

        int layer = context.HandsLayer;
        _scene = new GameObject($"TactileHands_{element.Id}") { layer = layer };
        _scene.transform.position = SceneOrigin + new Vector3(s_sceneCount++ * 50f, 0f, 0f);
        if (context.KeepAcrossScenes)
            UnityEngine.Object.DontDestroyOnLoad(_scene);

        _camera = CreateChild("Camera", _scene.transform, layer).AddComponent<Camera>();
        _camera.enabled = false; // 직접 Render() 호출
        _camera.orthographic = true;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _camera.cullingMask = 1 << layer;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 40f;
        _camera.allowHDR = false;
        _camera.allowMSAA = true;
        _camera.useOcclusionCulling = false;

        // Godot 렌더러와 같은 조명 (z 반전), 게임 환경광에 의존하지 않도록 정면 조명을 하나 더
        AddLight(new Vector3(35f, 30f, 0f), 0.9f, layer);
        AddLight(new Vector3(-20f, -150f, 0f), 0.3f, layer);
        AddLight(new Vector3(0f, 0f, 0f), 0.3f, layer);

        Mesh capsule = CapsuleMesh();
        Shader? shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Legacy Shaders/VertexLit");
        for (int h = 0; h < 2; h++)
        {
            _handRoots[h] = CreateChild($"{(Hand)h}Hand", _scene.transform, layer).transform;
            for (int i = 0; i < HandsVisual.SegmentsPerHand; i++)
            {
                int k = h * HandsVisual.SegmentsPerHand + i;
                _materials[k] = CreateMaterial(shader);
                _segments[k] = CreateMesh("Segment", _handRoots[h], capsule, _materials[k], layer).transform;
            }
        }
    }

    public void Render(HandsVisual visual)
    {
        _background.Apply(visual.Bounds, visual.Background);

        CoreRect bounds = visual.Bounds;
        var size = new Vector2Int(
            Math.Max(1, (int)Math.Round(bounds.Width * SuperSample)),
            Math.Max(1, (int)Math.Round(bounds.Height * SuperSample)));

        bool layoutChanged = size != _textureSize || _texture == null;
        if (layoutChanged)
        {
            _textureSize = size;
            RecreateTexture(size);
            PlaceCamera(visual, (float)size.x / size.y);

            _display.Clear();
            _display.Quad(bounds.X, bounds.Y, bounds.Width, bounds.Height, new Color32(255, 255, 255, 255));
            _display.Commit();
        }

        if (!layoutChanged && visual.Revision == _appliedRevision)
            return;
        _appliedRevision = visual.Revision;

        for (int h = 0; h < 2; h++)
        {
            var hand = (Hand)h;
            float side = hand == Hand.Left ? -1f : 1f;
            _handRoots[h].localPosition = new Vector3(side * _spacing + visual.GetOffset(hand), 0f, 0f);

            for (int i = 0; i < HandsVisual.SegmentsPerHand; i++)
            {
                int k = h * HandsVisual.SegmentsPerHand + i;
                HandSegment segment = visual.GetSegment(hand, i);
                PlaceCapsule(_segments[k], segment);
                if (segment.Color != _colors[k] || layoutChanged)
                {
                    _colors[k] = segment.Color;
                    _materials[k].color = segment.Color.ToUnity();
                }
            }
        }

        _camera.Render();
    }

    public void Dispose()
    {
        _background.Dispose();
        _display.Dispose();
        if (_node != null)
            UnityEngine.Object.Destroy(_node.gameObject);
        if (_scene != null)
            UnityEngine.Object.Destroy(_scene);
        foreach (Material material in _materials)
        {
            if (material != null)
                UnityEngine.Object.Destroy(material);
        }

        if (_texture != null)
        {
            _texture.Release();
            UnityEngine.Object.Destroy(_texture);
        }
    }

    private void RecreateTexture(Vector2Int size)
    {
        if (_texture != null)
        {
            _texture.Release();
            UnityEngine.Object.Destroy(_texture);
        }

        _texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
        {
            name = "TactileHands",
            antiAliasing = 4,
        };
        _texture.Create();
        _camera.targetTexture = _texture;
        _display.SetTexture(_texture);
    }

    /// <summary>Godot 렌더러의 PlaceCamera와 같은 계산.</summary>
    private void PlaceCamera(HandsVisual visual, float aspect)
    {
        CoreRect right = visual.GetRestBounds(Hand.Right);
        float halfHeight = right.Height / 2f * Margin;
        float centerY = right.Y + right.Height / 2f;

        float gap = right.Width * 0.15f;
        float minSpacing = gap / 2f - right.X;
        float halfWidthNeeded = (minSpacing + right.X + right.Width) * Margin;

        float visibleHalfWidth = halfHeight * aspect;
        if (halfWidthNeeded > visibleHalfWidth)
        {
            halfHeight = halfWidthNeeded / aspect;
            visibleHalfWidth = halfWidthNeeded;
        }

        float handCenter = right.X + right.Width / 2f;
        _spacing = Math.Max(minSpacing, visibleHalfWidth / 2f - handCenter);

        _camera.orthographicSize = halfHeight;
        _camera.transform.localPosition = new Vector3(0f, centerY, -20f);
        _camera.transform.localRotation = Quaternion.identity;
    }

    private static void PlaceCapsule(Transform t, in HandSegment segment)
    {
        var a = new Vector3(segment.From.X, segment.From.Y, -segment.From.Z);
        var b = new Vector3(segment.To.X, segment.To.Y, -segment.To.Z);
        Vector3 axis = b - a;
        float length = axis.magnitude;
        float r = segment.Radius;

        // 기본 캡슐: 축 Y, 반지름 0.5, 전체 높이 2 → 반지름 r, 전체 높이 length + 2r
        t.localPosition = (a + b) / 2f;
        t.localRotation = length > 1e-5f ? Quaternion.FromToRotation(Vector3.up, axis / length) : Quaternion.identity;
        t.localScale = new Vector3(r * 2f, (length + r * 2f) / 2f, r * 2f);
    }

    private void AddLight(Vector3 euler, float intensity, int layer)
    {
        var go = CreateChild("Light", _scene.transform, layer);
        go.transform.localRotation = Quaternion.Euler(euler);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = Color.white;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << layer;
    }

    private static Material CreateMaterial(Shader? shader)
    {
        var material = new Material(shader);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.3f);
        if (material.HasProperty("_GlossyReflections"))
        {
            // 게임 하늘/반사 프로브에 물들지 않게
            material.SetFloat("_GlossyReflections", 0f);
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        }

        return material;
    }

    private static GameObject CreateChild(string name, Transform parent, int layer)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateMesh(string name, Transform parent, Mesh mesh, Material material, int layer)
    {
        var go = CreateChild(name, parent, layer);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    private static Mesh CapsuleMesh()
    {
        if (s_capsule != null)
            return s_capsule;

        var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        s_capsule = temp.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.Destroy(temp);
        return s_capsule;
    }
}
