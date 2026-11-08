namespace Tactile.Core.Layout;

/// <summary>
/// 이 string 속성이 레이아웃 리소스(<see cref="LayoutAsset.Id"/>)를 가리킨다는 표시.
/// 에디터는 이 속성을 리소스 목록에서 고르게 하고, 리소스 이름을 바꾸거나 지울 때 함께 고칩니다.
/// 애드온 요소도 같은 방법으로 리소스를 쓸 수 있습니다.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AssetReferenceAttribute(string assetType) : Attribute
{
    /// <summary>가리킬 수 있는 리소스 종류 (<see cref="LayoutAsset.Type"/>).</summary>
    public string AssetType { get; } = assetType;
}
