using Tactile.Core.Layout.Elements;
using Tactile.Core.Primitives;

namespace Tactile.Core.Layout;

/// <summary>
/// 키뷰어 레이아웃 설정. 파일로 저장하고 불러오는 대상인 순수 데이터입니다.
/// <para>
/// 실행 중에는 바꾸지 않는 것을 전제로 합니다. 레이아웃이 바뀌면 <see cref="KeyViewer"/>를 새로 만드세요.
/// 좌표는 모두 레이아웃 픽셀 단위이며, 원점은 왼쪽 위이고 y는 아래로 증가합니다.
/// </para>
/// </summary>
public sealed class KeyViewerLayout
{
    /// <summary>현재 레이아웃 형식 버전.</summary>
    public const int CurrentVersion = 1;

    /// <summary>레이아웃 형식 버전. 구조가 바뀌어도 예전 파일을 읽기 위해 사용합니다.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>기준 캔버스 크기 (X = 너비, Y = 높이).</summary>
    public Vector2 Canvas { get; set; }

    /// <summary>
    /// 전체 배율. Core는 레이아웃 좌표 그대로 출력하며, 이 값은 엔진 연결 코드가 화면 좌표로 바꿀 때 사용합니다.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>화면 요소. 목록 순서대로 그리며, 나중 요소가 위에 그려집니다.</summary>
    public List<LayoutElement> Elements { get; set; } = [];

    /// <summary>
    /// 레이아웃에 함께 넣은 리소스 (이미지 등). 요소가 <see cref="LayoutAsset.Id"/>로 가리킵니다.
    /// 크기가 커질 수 있어 파일의 맨 뒤에 저장합니다.
    /// </summary>
    public List<LayoutAsset> Assets { get; set; } = [];

    /// <summary>이름으로 리소스를 찾습니다. 없으면 null.</summary>
    public LayoutAsset? FindAsset(string? id)
    {
        if (id is null)
            return null;
        foreach (LayoutAsset asset in Assets)
        {
            if (asset.Id == id)
                return asset;
        }

        return null;
    }
}
