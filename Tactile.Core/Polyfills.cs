// netstandard2.0에 없는 타입 중 컴파일러/분석기가 이름으로 찾는 것들을 내부용으로 정의합니다.

namespace System.Runtime.CompilerServices
{
    /// <summary><c>init</c> 접근자와 record를 쓰기 위해 필요합니다.</summary>
    internal static class IsExternalInit
    {
    }
}

namespace System.Runtime.Versioning
{
    /// <summary>플랫폼 호환성 분석기(CA1416)가 인식하는 특성입니다.</summary>
    [AttributeUsage(
        AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor |
        AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field |
        AttributeTargets.Method | AttributeTargets.Module | AttributeTargets.Property |
        AttributeTargets.Struct,
        AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute(string platformName) : Attribute
    {
        public string PlatformName { get; } = platformName;
    }
}

namespace AOT
{
    /// <summary>
    /// Unity IL2CPP는 네이티브로 넘기는 콜백에 이 특성(이름으로 인식)이 붙은 static 메서드만 허용합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class MonoPInvokeCallbackAttribute(Type type) : Attribute
    {
        public Type Type { get; } = type;
    }
}
