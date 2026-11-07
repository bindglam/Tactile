using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Tactile.Core.Elements;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Primitives;

namespace Tactile.Core.Layout.Serialization;

/// <summary>
/// 레이아웃을 JSON 파일로 저장하고 불러옵니다.
/// <code>
/// LayoutSerializer.SaveFile(layout, "layout.json");
/// KeyViewerLayout loaded = LayoutSerializer.LoadFile("layout.json");
/// </code>
/// <para>
/// 요소는 <c>"type"</c>에 등록표(<see cref="ElementRegistry"/>)의 종류 ID를 적어 구분하고, 나머지는 공개 속성(get과 public set이 있는 것)을
/// 이름 그대로(첫 글자 소문자) 저장합니다. 그래서 애드온 요소도 등록만 하면 따로 코드 없이 저장됩니다.
/// 저장하지 않을 속성에는 <see cref="LayoutIgnoreAttribute"/>를 붙이세요. 요소 클래스에는 매개변수 없는 생성자가 있어야 합니다.
/// </para>
/// <para>
/// 불러올 때 파일에 없는 속성은 클래스 기본값을 그대로 씁니다 (스타일 안의 일부 값만 적어도 됨).
/// 모르는 속성은 무시하고, 등록되지 않은 종류의 요소는 <see cref="UnknownElement"/>로 원래 내용을 보존해 다시 저장할 때 그대로 씁니다.
/// </para>
/// <para>
/// 지원하는 값: bool, 정수, 실수, string, enum(이름), <see cref="Color"/>(<c>"#RRGGBBAA"</c>), <see cref="Vector2"/>, <see cref="Vector3"/>,
/// <see cref="Rect"/>, <see cref="InputBinding"/>(<c>"Keyboard.A"</c>), <see cref="List{T}"/>, 배열, nullable, 공개 속성을 가진 클래스·구조체.
/// </para>
/// </summary>
public static class LayoutSerializer
{
    /// <summary>레이아웃을 JSON 글로 만듭니다.</summary>
    /// <param name="elements">요소 종류 등록표. null이면 <see cref="ElementRegistry.CreateDefault"/></param>
    /// <exception cref="InvalidOperationException">등록되지 않은 종류의 요소가 있을 때</exception>
    public static string Save(KeyViewerLayout layout, ElementRegistry? elements = null)
    {
        if (layout is null)
            throw new ArgumentNullException(nameof(layout));

        var context = new Context(elements ?? ElementRegistry.CreateDefault());
        return JsonWriter.Write(context.Write(layout, ""));
    }

    /// <summary>JSON 글에서 레이아웃을 읽습니다.</summary>
    /// <param name="elements">요소 종류 등록표. null이면 <see cref="ElementRegistry.CreateDefault"/></param>
    /// <exception cref="LayoutFormatException">형식이나 값이 올바르지 않을 때</exception>
    public static KeyViewerLayout Load(string json, ElementRegistry? elements = null)
    {
        JsonNode root = JsonReader.Parse(json);
        if (root is not JsonObject obj)
            throw new LayoutFormatException("레이아웃 파일은 { }로 감싼 객체여야 합니다", "", root.Line);

        // 버전을 먼저 확인 (새 형식을 옛 코드로 잘못 읽지 않도록)
        if (obj.Get("version") is JsonNumber versionNode)
        {
            double version = versionNode.ToDouble();
            if (version > KeyViewerLayout.CurrentVersion)
            {
                throw new LayoutFormatException(
                    $"이 레이아웃은 더 새로운 형식(버전 {version})입니다. 지원하는 버전은 {KeyViewerLayout.CurrentVersion}까지입니다 ({versionNode.Line}번째 줄)",
                    "version", versionNode.Line);
            }
        }

        var context = new Context(elements ?? ElementRegistry.CreateDefault());
        return (KeyViewerLayout)context.Read(obj, typeof(KeyViewerLayout), null, "")!;
    }

    /// <summary>레이아웃을 파일로 저장합니다 (UTF-8). 쓰는 도중 실패해도 원래 파일이 깨지지 않도록 임시 파일에 먼저 씁니다.</summary>
    public static void SaveFile(KeyViewerLayout layout, string path, ElementRegistry? elements = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        string json = Save(layout, elements);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temp = path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }

    /// <summary>파일에서 레이아웃을 읽습니다.</summary>
    /// <exception cref="LayoutFormatException">형식이나 값이 올바르지 않을 때</exception>
    public static KeyViewerLayout LoadFile(string path, ElementRegistry? elements = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        return Load(File.ReadAllText(path, Encoding.UTF8), elements);
    }

    /// <summary>레이아웃을 깊이 복사합니다 (저장했다가 다시 읽은 것과 같음). 에디터의 되돌리기 등에 사용합니다.</summary>
    public static KeyViewerLayout Clone(KeyViewerLayout layout, ElementRegistry? elements = null)
    {
        ElementRegistry registry = elements ?? ElementRegistry.CreateDefault();
        return Load(Save(layout, registry), registry);
    }

    // ---------------------------------------------------------------------------------------------

    private sealed class Context(ElementRegistry elements)
    {
        private const string TypeKey = "type";

        public JsonNode Write(object? value, string path)
        {
            if (value is null)
                return new JsonNull();

            Type type = value.GetType();
            if (value is LayoutElement element)
                return WriteElement(element, path);

            if (type == typeof(string))
                return new JsonString((string)value);
            if (type == typeof(bool))
                return new JsonBool((bool)value);
            if (type == typeof(float))
                return Number((float)value);
            if (type == typeof(double))
                return Number((double)value);
            if (type.IsPrimitive)
                return new JsonNumber(Convert.ToString(value, CultureInfo.InvariantCulture)!);
            if (type.IsEnum)
                return new JsonString(value.ToString());

            if (value is Color color)
                return WriteColor(color);
            if (value is Vector2 v2)
                return Obj(("x", Number(v2.X)), ("y", Number(v2.Y)));
            if (value is Vector3 v3)
                return Obj(("x", Number(v3.X)), ("y", Number(v3.Y)), ("z", Number(v3.Z)));
            if (value is Rect rect)
                return Obj(("x", Number(rect.X)), ("y", Number(rect.Y)), ("width", Number(rect.Width)), ("height", Number(rect.Height)));
            if (value is InputBinding binding)
                return new JsonString(binding.ToString());

            if (value is IList list)
            {
                var array = new JsonArray();
                for (int i = 0; i < list.Count; i++)
                    array.Items.Add(Write(list[i], $"{path}[{i}]"));
                return array;
            }

            return WriteProperties(value, new JsonObject(), path);
        }

        private JsonNode WriteElement(LayoutElement element, string path)
        {
            if (element is UnknownElement unknown)
            {
                var data = (JsonObject)unknown.Data.Clone();
                data.Set("id", new JsonString(unknown.Id));
                data.Set("bounds", Write(unknown.Bounds, path));
                return data;
            }

            if (!elements.TryGetTypeId(element.GetType(), out string typeId))
            {
                throw new InvalidOperationException(
                    $"'{element.Id}' 요소의 종류({element.GetType().Name})가 등록표에 없어 저장할 수 없습니다 ({path}). ElementRegistry에 등록하세요.");
            }

            var obj = new JsonObject();
            obj.Set(TypeKey, new JsonString(typeId));
            return WriteProperties(element, obj, path);
        }

        private JsonObject WriteProperties(object value, JsonObject obj, string path)
        {
            foreach (PropertyInfo property in Properties(value.GetType()))
            {
                string name = JsonName(property);
                obj.Set(name, Write(property.GetValue(value), Join(path, name)));
            }

            return obj;
        }

        public object? Read(JsonNode node, Type type, object? existing, string path)
        {
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable is not null)
                return node is JsonNull ? null : Read(node, nullable, existing, path);

            if (node is JsonNull)
            {
                if (type.IsValueType)
                    throw Fail("null을 넣을 수 없는 값입니다", node, path);
                return null;
            }

            if (typeof(LayoutElement).IsAssignableFrom(type))
                return ReadElement(node, type, path);

            if (type == typeof(string))
                return node is JsonString s ? s.Value : throw Fail("문자열이어야 합니다", node, path);
            if (type == typeof(bool))
                return node is JsonBool b ? b.Value : throw Fail("true 또는 false여야 합니다", node, path);
            if (type == typeof(float))
                return (float)ReadNumber(node, path);
            if (type == typeof(double))
                return ReadNumber(node, path);
            if (type.IsPrimitive)
                return ReadInteger(node, type, path);
            if (type.IsEnum)
                return ReadEnum(node, type, path);

            if (type == typeof(Color))
                return ReadColor(node, path);
            if (type == typeof(Vector2))
            {
                JsonObject o = Expect(node, path);
                return new Vector2(Field(o, "x", path), Field(o, "y", path));
            }

            if (type == typeof(Vector3))
            {
                JsonObject o = Expect(node, path);
                return new Vector3(Field(o, "x", path), Field(o, "y", path), Field(o, "z", path));
            }

            if (type == typeof(Rect))
            {
                JsonObject o = Expect(node, path);
                return new Rect(Field(o, "x", path), Field(o, "y", path), Field(o, "width", path), Field(o, "height", path));
            }

            if (type == typeof(InputBinding))
                return ReadBinding(node, path);

            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
                return ReadList(node, type, path);

            return ReadProperties(node, type, existing, path);
        }

        private LayoutElement ReadElement(JsonNode node, Type declared, string path)
        {
            JsonObject obj = Expect(node, path);
            if (obj.Get(TypeKey) is not JsonString typeNode || typeNode.Value.Length == 0)
                throw Fail("요소에는 종류(\"type\")가 있어야 합니다", node, path);

            string typeId = typeNode.Value;
            if (!elements.TryGetElementType(typeId, out Type elementType))
            {
                // 등록되지 않은 종류: 내용을 그대로 보존
                var unknown = new UnknownElement(typeId, (JsonObject)obj.Clone());
                if (obj.Get("id") is JsonString id)
                    unknown.Id = id.Value;
                if (obj.Get("bounds") is JsonObject bounds)
                    unknown.Bounds = (Rect)Read(bounds, typeof(Rect), null, Join(path, "bounds"))!;
                return unknown;
            }

            if (!declared.IsAssignableFrom(elementType))
                throw Fail($"'{typeId}' 요소는 여기에 들어갈 수 없습니다 ({declared.Name}이어야 함)", node, path);

            return (LayoutElement)ReadProperties(obj, elementType, null, path)!;
        }

        private object ReadProperties(JsonNode node, Type type, object? existing, string path)
        {
            JsonObject obj = Expect(node, path);
            object target = existing ?? Create(type, node, path);

            foreach (PropertyInfo property in Properties(type))
            {
                string name = JsonName(property);
                JsonNode? value = obj.Get(name);
                if (value is null)
                    continue; // 파일에 없으면 기본값 유지

                // 스타일 같은 객체 값은 기본값 위에 파일에 있는 속성만 덮어씀 (구조체는 복사본이 돌아오므로 다시 넣음)
                Type valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object? current = IsPlainObject(valueType) ? property.GetValue(target) : null;

                property.SetValue(target, Read(value, property.PropertyType, current, Join(path, name)));
            }

            return target;
        }

        private object ReadList(JsonNode node, Type type, string path)
        {
            if (node is not JsonArray array)
                throw Fail("배열([ ])이어야 합니다", node, path);

            Type itemType = ItemType(type)!;
            if (type.IsArray)
            {
                var result = Array.CreateInstance(itemType, array.Items.Count);
                for (int i = 0; i < array.Items.Count; i++)
                    result.SetValue(Read(array.Items[i], itemType, null, $"{path}[{i}]"), i);
                return result;
            }

            var list = (IList)Activator.CreateInstance(type)!;
            for (int i = 0; i < array.Items.Count; i++)
                list.Add(Read(array.Items[i], itemType, null, $"{path}[{i}]"));
            return list;
        }

        // ---- 값 형식별 ----

        private static JsonNode WriteColor(Color color)
        {
            // 0~255 단위로 정확히 나타낼 수 있으면 #RRGGBB(AA), 아니면 [r, g, b, a]
            if (TryByte(color.R, out byte r) && TryByte(color.G, out byte g) && TryByte(color.B, out byte b) && TryByte(color.A, out byte a)
                && Color.FromRgba(r, g, b, a) == color)
            {
                string hex = a == 255 ? $"#{r:X2}{g:X2}{b:X2}" : $"#{r:X2}{g:X2}{b:X2}{a:X2}";
                return new JsonString(hex);
            }

            var array = new JsonArray();
            array.Items.Add(Number(color.R));
            array.Items.Add(Number(color.G));
            array.Items.Add(Number(color.B));
            array.Items.Add(Number(color.A));
            return array;
        }

        private static bool TryByte(float channel, out byte value)
        {
            float scaled = channel * 255f;
            float rounded = (float)Math.Round(scaled);
            value = (byte)Math.Max(0f, Math.Min(255f, rounded));
            return Math.Abs(scaled - rounded) < 1e-3f && rounded >= 0f && rounded <= 255f;
        }

        private static Color ReadColor(JsonNode node, string path)
        {
            if (node is JsonString s)
            {
                try
                {
                    return Color.FromHex(s.Value);
                }
                catch (FormatException)
                {
                    throw Fail($"색은 \"#RRGGBB\" 또는 \"#RRGGBBAA\" 형식이어야 합니다: {s.Value}", node, path);
                }
            }

            if (node is JsonArray array && array.Items.Count is 3 or 4)
            {
                float Channel(int i) => (float)ReadNumber(array.Items[i], $"{path}[{i}]");
                return new Color(Channel(0), Channel(1), Channel(2), array.Items.Count == 4 ? Channel(3) : 1f);
            }

            throw Fail("색은 \"#RRGGBBAA\" 또는 [r, g, b, a] (0~1) 형식이어야 합니다", node, path);
        }

        private static InputBinding ReadBinding(JsonNode node, string path)
        {
            if (node is not JsonString s)
                throw Fail("입력은 \"Keyboard.A\" 형식의 문자열이어야 합니다", node, path);

            int dot = s.Value.IndexOf('.');
            if (dot <= 0 || dot == s.Value.Length - 1)
                throw Fail($"입력은 \"Keyboard.A\" 형식이어야 합니다: {s.Value}", node, path);

            string sourceName = s.Value.Substring(0, dot);
            string codeName = s.Value.Substring(dot + 1);
            if (!TryParseEnum(typeof(InputSource), sourceName, out object? source))
                throw Fail($"알 수 없는 입력 장치입니다: {sourceName}", node, path);

            int code;
            if ((InputSource)source! == InputSource.Keyboard && TryParseEnum(typeof(KeyCode), codeName, out object? key))
                code = Convert.ToInt32(key, CultureInfo.InvariantCulture);
            else if (!int.TryParse(codeName, NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
                throw Fail($"알 수 없는 키입니다: {codeName}", node, path);

            return new InputBinding((InputSource)source, code);
        }

        private static object ReadEnum(JsonNode node, Type type, string path)
        {
            if (node is JsonString s && TryParseEnum(type, s.Value, out object? value))
                return value!;
            if (node is JsonNumber)
                return Enum.ToObject(type, Convert.ToInt64(ReadNumber(node, path), CultureInfo.InvariantCulture));

            string names = string.Join(", ", Enum.GetNames(type));
            throw Fail($"{type.Name} 값이어야 합니다 ({names})", node, path);
        }

        private static bool TryParseEnum(Type type, string text, out object? value)
        {
            value = null;
            if (text.Length == 0 || char.IsDigit(text[0]) || text[0] == '-')
                return false; // 이름만 허용 (숫자 글자는 JSON 숫자로)
            try
            {
                value = Enum.Parse(type, text, ignoreCase: true);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static double ReadNumber(JsonNode node, string path)
        {
            if (node is JsonNumber n)
                return n.ToDouble();
            if (node is JsonString s)
            {
                // JSON에는 NaN/무한대가 없으므로 문자열로 저장함
                switch (s.Value)
                {
                    case "NaN": return double.NaN;
                    case "Infinity": return double.PositiveInfinity;
                    case "-Infinity": return double.NegativeInfinity;
                }
            }

            throw Fail("숫자여야 합니다", node, path);
        }

        private static object ReadInteger(JsonNode node, Type type, string path)
        {
            double value = ReadNumber(node, path);
            if (Math.Abs(value - Math.Round(value)) > 0)
                throw Fail("정수여야 합니다", node, path);
            try
            {
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw Fail($"{type.Name} 범위를 벗어났습니다: {value}", node, path);
            }
        }

        private static JsonNode Number(float value) => Number((double)value, value.ToString("R", CultureInfo.InvariantCulture));

        private static JsonNode Number(double value) => Number(value, value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>JSON에는 NaN/무한대가 없으므로 문자열로 저장합니다 (<see cref="ReadNumber"/>가 읽음).</summary>
        private static JsonNode Number(double value, string text) =>
            double.IsNaN(value) ? new JsonString("NaN")
            : double.IsPositiveInfinity(value) ? new JsonString("Infinity")
            : double.IsNegativeInfinity(value) ? new JsonString("-Infinity")
            : new JsonNumber(text);

        private static JsonObject Obj(params (string Name, JsonNode Value)[] members)
        {
            var obj = new JsonObject();
            foreach ((string name, JsonNode value) in members)
                obj.Set(name, value);
            return obj;
        }

        private static JsonObject Expect(JsonNode node, string path) =>
            node as JsonObject ?? throw Fail("객체({ })여야 합니다", node, path);

        private static float Field(JsonObject obj, string name, string path) =>
            obj.Get(name) is { } value ? (float)ReadNumber(value, Join(path, name)) : 0f;

        private static object Create(Type type, JsonNode node, string path)
        {
            if (type.IsAbstract || type.IsInterface)
                throw Fail($"{type.Name}은(는) 직접 만들 수 없는 형식입니다", node, path);
            try
            {
                return Activator.CreateInstance(type)!;
            }
            catch (MissingMethodException)
            {
                throw Fail($"{type.Name}에 매개변수 없는 생성자가 없어 불러올 수 없습니다", node, path);
            }
        }

        private static LayoutFormatException Fail(string message, JsonNode node, string path)
        {
            string where = path.Length > 0 ? $"{path}: " : "";
            string line = node.Line > 0 ? $" ({node.Line}번째 줄)" : "";
            return new LayoutFormatException(where + message + line, path, node.Line);
        }

        private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
    }

    // ---- 형식 정보 (캐시) ----

    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>저장할 속성: 공개 인스턴스 속성 중 get과 public set이 있고 무시 표시가 없는 것. 부모 클래스 속성이 먼저.</summary>
    private static PropertyInfo[] Properties(Type type)
    {
        lock (PropertyCache)
        {
            if (PropertyCache.TryGetValue(type, out PropertyInfo[]? cached))
                return cached;

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetSetMethod(nonPublic: false) is not null && p.GetIndexParameters().Length == 0
                            && !p.IsDefined(typeof(LayoutIgnoreAttribute), inherit: true))
                .OrderBy(p => Depth(p.DeclaringType!))
                .ThenBy(p => p.MetadataToken)
                .ToArray();
            PropertyCache[type] = properties;
            return properties;
        }
    }

    private static int Depth(Type type)
    {
        int depth = 0;
        for (Type? t = type.BaseType; t is not null; t = t.BaseType)
            depth++;
        return depth;
    }

    private static string JsonName(PropertyInfo property) =>
        char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);

    private static Type? ItemType(Type type) =>
        type.IsArray ? type.GetElementType()
        : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0]
        : null;

    /// <summary>속성 단위로 읽고 쓰는 일반 클래스·구조체인지 (기본 지원 값 형식이 아닌 것).</summary>
    private static bool IsPlainObject(Type type) =>
        !type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(Color) && type != typeof(Vector2)
        && type != typeof(Vector3) && type != typeof(Rect) && type != typeof(InputBinding)
        && !typeof(LayoutElement).IsAssignableFrom(type) && ItemType(type) is null;
}
