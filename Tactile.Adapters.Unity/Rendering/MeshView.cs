using UnityEngine;

namespace Tactile.Adapters.Unity.Rendering;

/// <summary>
/// <see cref="CanvasRenderer"/>에 직접 메시를 넣어 그리는 UI 조각. (UnityEngine.UI 없이 엔진 모듈만으로 그리기 위함)
/// 정점 목록을 채운 뒤 <see cref="Commit"/>하면 반영됩니다. 목록과 메시는 재사용합니다.
/// </summary>
internal sealed class MeshView : IDisposable
{
    private static Material? s_material;

    private readonly GameObject _object;
    private readonly CanvasRenderer _renderer;
    private readonly Mesh _mesh = new() { name = "TactileMesh" };
    private Texture? _texture;

    public readonly List<Vector3> Positions = new();
    public readonly List<Color32> Colors = new();
    public readonly List<Vector2> Uvs = new();
    public readonly List<int> Triangles = new();

    public MeshView(string name, Transform parent)
    {
        _object = UnityRenderContext.CreateNode(name, parent).gameObject;
        _renderer = _object.AddComponent<CanvasRenderer>();
        _mesh.MarkDynamic();
        s_material ??= Canvas.GetDefaultCanvasMaterial();
        _renderer.materialCount = 1;
        _renderer.SetMaterial(s_material, 0);
        SetTexture(null);
    }

    public GameObject GameObject => _object;

    public void SetTexture(Texture? texture)
    {
        texture ??= Texture2D.whiteTexture;
        if (_texture == texture)
            return;
        _texture = texture;
        _renderer.SetTexture(texture);
    }

    public void Clear()
    {
        Positions.Clear();
        Colors.Clear();
        Uvs.Clear();
        Triangles.Clear();
    }

    /// <summary>레이아웃 좌표 (x, y)의 정점을 추가하고 번호를 반환합니다.</summary>
    public int Add(float x, float y, Color32 color, Vector2 uv = default)
    {
        Positions.Add(new Vector3(x, -y, 0f));
        Colors.Add(color);
        Uvs.Add(uv);
        return Positions.Count - 1;
    }

    public void Quad(float x, float y, float width, float height, Color32 color,
        float u0 = 0f, float v0 = 0f, float u1 = 1f, float v1 = 1f)
    {
        int a = Add(x, y, color, new Vector2(u0, v1));
        int b = Add(x + width, y, color, new Vector2(u1, v1));
        int c = Add(x + width, y + height, color, new Vector2(u1, v0));
        int d = Add(x, y + height, color, new Vector2(u0, v0));
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    public void Triangle(int a, int b, int c)
    {
        Triangles.Add(a);
        Triangles.Add(b);
        Triangles.Add(c);
    }

    /// <summary>채운 정점을 메시에 반영합니다.</summary>
    public void Commit()
    {
        _mesh.Clear();
        _mesh.SetVertices(Positions);
        _mesh.SetColors(Colors);
        _mesh.SetUVs(0, Uvs);
        _mesh.SetTriangles(Triangles, 0, calculateBounds: true);
        _renderer.SetMesh(_mesh);
    }

    public void SetVisible(bool visible)
    {
        if (_object.activeSelf != visible)
            _object.SetActive(visible);
    }

    public void Dispose()
    {
        if (_object != null)
            UnityEngine.Object.Destroy(_object);
        UnityEngine.Object.Destroy(_mesh);
    }
}
