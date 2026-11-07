using Tactile.Core.Layout.Serialization;

namespace Tactile.Core.Layout.Elements;

/// <summary>
/// 불러올 때 등록되지 않은 종류였던 요소 (애드온이 빠진 레이아웃 등).
/// 원래 내용을 그대로 들고 있다가 다시 저장할 때 그대로 씁니다. <see cref="LayoutElement.Id"/>와
/// <see cref="LayoutElement.Bounds"/>는 바꿀 수 있고(에디터에서 옮기기 등), 바꾼 값이 저장됩니다.
/// 키뷰어에서는 그려지지 않고 <see cref="KeyViewer.UnsupportedElements"/>에 들어갑니다.
/// </summary>
public sealed class UnknownElement : LayoutElement
{
    internal UnknownElement(string typeId, JsonObject data)
    {
        TypeId = typeId;
        Data = data;
    }

    /// <summary>파일에 적혀 있던 종류 ID.</summary>
    public string TypeId { get; }

    /// <summary>원래 내용 (type, id, bounds 포함).</summary>
    internal JsonObject Data { get; }
}
