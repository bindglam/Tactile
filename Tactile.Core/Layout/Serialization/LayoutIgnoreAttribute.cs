namespace Tactile.Core.Layout.Serialization;

/// <summary>
/// 이 속성은 레이아웃 파일에 저장하지 않습니다. 실행 중에만 쓰는 값이나 다른 속성에서 계산되는 값에 붙이세요.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LayoutIgnoreAttribute : Attribute
{
}
