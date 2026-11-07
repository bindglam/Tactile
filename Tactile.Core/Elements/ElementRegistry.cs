using Tactile.Core.Elements.Builtin;
using Tactile.Core.Layout.Elements;

namespace Tactile.Core.Elements;

/// <summary>
/// 요소 종류 등록표. 레이아웃 요소 클래스, 종류 ID, 요소 로직을 만드는 함수를 묶어 둡니다.
/// <code>
/// var elements = ElementRegistry.CreateDefault();
/// elements.Register&lt;GraphElement&gt;("myaddon:graph", (element, context) => new GraphBehavior(element));
/// </code>
/// <para>
/// 종류 ID는 레이아웃 파일에서 요소 종류를 구분하는 이름입니다. 다른 애드온과 겹치지 않도록 <c>"애드온이름:요소이름"</c> 형식을 권장합니다.
/// 전역 등록표가 아니라 인스턴스이므로, 같은 Core를 쓰는 여러 모드가 서로의 등록에 영향을 주지 않습니다.
/// 한 스레드에서 등록을 마친 뒤 사용하세요.
/// </para>
/// </summary>
public sealed class ElementRegistry
{
    /// <summary>기본 키 요소(<see cref="KeyElement"/>)의 종류 ID.</summary>
    public const string KeyTypeId = "tactile:key";

    /// <summary>기본 통계 요소(<see cref="StatElement"/>)의 종류 ID.</summary>
    public const string StatTypeId = "tactile:stat";

    /// <summary>기본 손 요소(<see cref="HandsElement"/>)의 종류 ID.</summary>
    public const string HandsTypeId = "tactile:hands";

    private readonly Dictionary<Type, Entry> _byType = new();
    private readonly Dictionary<string, Entry> _byTypeId = new(StringComparer.Ordinal);

    /// <summary>기본 요소(키, 통계, 손)가 등록된 등록표를 만듭니다.</summary>
    public static ElementRegistry CreateDefault()
    {
        var registry = new ElementRegistry();
        registry.Register<KeyElement>(KeyTypeId, (element, context) => new KeyBehavior(element, context));
        registry.Register<StatElement>(StatTypeId, (element, context) => new StatBehavior(element, context));
        registry.Register<HandsElement>(HandsTypeId, (element, context) => new HandsBehavior(element));
        return registry;
    }

    /// <summary>등록된 종류 ID 목록.</summary>
    public IEnumerable<string> TypeIds => _byTypeId.Keys;

    /// <summary>요소 종류를 등록합니다.</summary>
    /// <typeparam name="TElement">레이아웃 요소 클래스</typeparam>
    /// <param name="typeId">종류 ID. 예: <c>"myaddon:graph"</c></param>
    /// <param name="factory">요소 로직을 만드는 함수. 레이아웃마다 요소 하나당 한 번 호출됩니다.</param>
    /// <exception cref="ArgumentException">종류 ID나 요소 클래스가 이미 등록된 경우</exception>
    public void Register<TElement>(string typeId, Func<TElement, ElementBuildContext, ElementBehavior> factory)
        where TElement : LayoutElement
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));
        ValidateTypeId(typeId);

        Type type = typeof(TElement);
        if (type.IsAbstract)
            throw new ArgumentException($"추상 클래스는 등록할 수 없습니다: {type.Name}");
        if (_byTypeId.ContainsKey(typeId))
            throw new ArgumentException($"이미 등록된 종류 ID입니다: {typeId}");
        if (_byType.ContainsKey(type))
            throw new ArgumentException($"이미 등록된 요소 클래스입니다: {type.FullName}");

        var entry = new Entry(typeId, type, (element, context) =>
            factory((TElement)element, context)
            ?? throw new InvalidOperationException($"'{typeId}' 요소 로직을 만드는 함수가 null을 반환했습니다."));

        _byType.Add(type, entry);
        _byTypeId.Add(typeId, entry);
    }

    /// <summary>요소 클래스가 등록되어 있는지 확인합니다.</summary>
    public bool IsRegistered(Type elementType) => _byType.ContainsKey(elementType);

    /// <summary>요소 클래스의 종류 ID를 찾습니다. (레이아웃 저장 시 사용)</summary>
    public bool TryGetTypeId(Type elementType, out string typeId)
    {
        if (_byType.TryGetValue(elementType, out Entry? entry))
        {
            typeId = entry.TypeId;
            return true;
        }

        typeId = "";
        return false;
    }

    /// <summary>종류 ID에 해당하는 요소 클래스를 찾습니다. (레이아웃 불러오기 시 사용)</summary>
    public bool TryGetElementType(string typeId, out Type elementType)
    {
        if (_byTypeId.TryGetValue(typeId, out Entry? entry))
        {
            elementType = entry.ElementType;
            return true;
        }

        elementType = typeof(LayoutElement);
        return false;
    }

    /// <summary>요소 로직을 만듭니다. 등록되지 않은 요소면 null.</summary>
    internal ElementBehavior? Create(LayoutElement element, ElementBuildContext context) =>
        _byType.TryGetValue(element.GetType(), out Entry? entry) ? entry.Create(element, context) : null;

    private static void ValidateTypeId(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            throw new ArgumentException("종류 ID가 비어 있습니다.", nameof(typeId));
        foreach (char c in typeId)
        {
            if (char.IsWhiteSpace(c))
                throw new ArgumentException($"종류 ID에 공백을 넣을 수 없습니다: '{typeId}'", nameof(typeId));
        }
    }

    private sealed class Entry(string typeId, Type elementType, Func<LayoutElement, ElementBuildContext, ElementBehavior> create)
    {
        public string TypeId { get; } = typeId;
        public Type ElementType { get; } = elementType;
        public Func<LayoutElement, ElementBuildContext, ElementBehavior> Create { get; } = create;
    }
}
