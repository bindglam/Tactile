using System.Globalization;
using System.Text;

namespace Tactile.Core.Layout.Serialization;

// 레이아웃 파일용 최소 JSON 구현. netstandard2.0에는 System.Text.Json이 없고, Core는 외부 패키지를 쓰지 않으므로 직접 구현합니다.

/// <summary>JSON 값.</summary>
internal abstract class JsonNode
{
    /// <summary>파일에서 이 값이 시작한 위치 (오류 메시지용, 코드로 만든 값은 0).</summary>
    public int Line { get; set; }

    public abstract JsonNode Clone();
}

internal sealed class JsonNull : JsonNode
{
    public override JsonNode Clone() => new JsonNull { Line = Line };
}

internal sealed class JsonBool(bool value) : JsonNode
{
    public bool Value { get; } = value;

    public override JsonNode Clone() => new JsonBool(Value) { Line = Line };
}

/// <summary>숫자. 정밀도를 잃지 않도록 글자 그대로 들고 있습니다.</summary>
internal sealed class JsonNumber(string text) : JsonNode
{
    public string Text { get; } = text;

    public double ToDouble() => double.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);

    public override JsonNode Clone() => new JsonNumber(Text) { Line = Line };
}

internal sealed class JsonString(string value) : JsonNode
{
    public string Value { get; } = value;

    public override JsonNode Clone() => new JsonString(Value) { Line = Line };
}

internal sealed class JsonArray : JsonNode
{
    public List<JsonNode> Items { get; } = new();

    public override JsonNode Clone()
    {
        var copy = new JsonArray { Line = Line };
        foreach (JsonNode item in Items)
            copy.Items.Add(item.Clone());
        return copy;
    }
}

/// <summary>객체. 쓴 순서를 유지합니다.</summary>
internal sealed class JsonObject : JsonNode
{
    private readonly List<KeyValuePair<string, JsonNode>> _members = new();

    public IReadOnlyList<KeyValuePair<string, JsonNode>> Members => _members;

    /// <summary>이름으로 찾습니다 (대소문자 무시).</summary>
    public JsonNode? Get(string name)
    {
        foreach (KeyValuePair<string, JsonNode> member in _members)
        {
            if (string.Equals(member.Key, name, StringComparison.OrdinalIgnoreCase))
                return member.Value;
        }

        return null;
    }

    /// <summary>값을 넣습니다. 같은 이름(대소문자 무시)이 있으면 그 자리에서 바꿉니다.</summary>
    public void Set(string name, JsonNode value)
    {
        for (int i = 0; i < _members.Count; i++)
        {
            if (string.Equals(_members[i].Key, name, StringComparison.OrdinalIgnoreCase))
            {
                _members[i] = new KeyValuePair<string, JsonNode>(_members[i].Key, value);
                return;
            }
        }

        _members.Add(new KeyValuePair<string, JsonNode>(name, value));
    }

    public override JsonNode Clone()
    {
        var copy = new JsonObject { Line = Line };
        foreach (KeyValuePair<string, JsonNode> member in _members)
            copy._members.Add(new KeyValuePair<string, JsonNode>(member.Key, member.Value.Clone()));
        return copy;
    }
}

/// <summary>JSON 글을 읽습니다. 사람이 고친 파일을 위해 주석(<c>//</c>, <c>/* */</c>)과 끝의 쉼표를 허용합니다.</summary>
internal sealed class JsonReader
{
    private readonly string _text;
    private int _pos;
    private int _line = 1;
    private int _lineStart;

    private JsonReader(string text) => _text = text;

    public static JsonNode Parse(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var reader = new JsonReader(text);
        // UTF-8 BOM
        if (text.Length > 0 && text[0] == '﻿')
            reader._pos = 1;

        JsonNode root = reader.ReadValue();
        reader.SkipWhitespace();
        if (reader._pos < text.Length)
            throw reader.Error("값 뒤에 알 수 없는 내용이 있습니다");
        return root;
    }

    private JsonNode ReadValue()
    {
        SkipWhitespace();
        if (_pos >= _text.Length)
            throw Error("값이 있어야 하는데 파일이 끝났습니다");

        int line = _line;
        char c = _text[_pos];
        JsonNode node = c switch
        {
            '{' => ReadObject(),
            '[' => ReadArray(),
            '"' => new JsonString(ReadString()),
            't' => ReadWord("true", new JsonBool(true)),
            'f' => ReadWord("false", new JsonBool(false)),
            'n' => ReadWord("null", new JsonNull()),
            _ when c == '-' || (c >= '0' && c <= '9') => ReadNumber(),
            _ => throw Error($"알 수 없는 문자 '{c}'"),
        };
        node.Line = line;
        return node;
    }

    private JsonObject ReadObject()
    {
        var obj = new JsonObject();
        _pos++; // {
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '}')
            {
                _pos++;
                return obj;
            }

            if (Peek() != '"')
                throw Error("속성 이름(\"...\")이 있어야 합니다");
            string name = ReadString();
            SkipWhitespace();
            Expect(':');
            obj.Set(name, ReadValue());

            SkipWhitespace();
            char next = Peek();
            if (next == ',')
                _pos++;
            else if (next != '}')
                throw Error("',' 또는 '}'가 있어야 합니다");
        }
    }

    private JsonArray ReadArray()
    {
        var array = new JsonArray();
        _pos++; // [
        while (true)
        {
            SkipWhitespace();
            if (Peek() == ']')
            {
                _pos++;
                return array;
            }

            array.Items.Add(ReadValue());
            SkipWhitespace();
            char next = Peek();
            if (next == ',')
                _pos++;
            else if (next != ']')
                throw Error("',' 또는 ']'가 있어야 합니다");
        }
    }

    private string ReadString()
    {
        _pos++; // "
        var sb = new StringBuilder();
        while (true)
        {
            if (_pos >= _text.Length)
                throw Error("문자열이 닫히지 않았습니다");

            char c = _text[_pos++];
            if (c == '"')
                return sb.ToString();
            if (c == '\n')
                throw Error("문자열 안에서 줄을 바꿀 수 없습니다 (\\n을 쓰세요)");
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            if (_pos >= _text.Length)
                throw Error("문자열이 닫히지 않았습니다");
            char e = _text[_pos++];
            switch (e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (_pos + 4 > _text.Length ||
                        !int.TryParse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        throw Error("\\u 뒤에는 16진수 4자리가 있어야 합니다");
                    sb.Append((char)code);
                    _pos += 4;
                    break;
                default:
                    throw Error($"알 수 없는 이스케이프 '\\{e}'");
            }
        }
    }

    private JsonNumber ReadNumber()
    {
        int start = _pos;
        if (Peek() == '-')
            _pos++;
        while (_pos < _text.Length && "0123456789.eE+-".IndexOf(_text[_pos]) >= 0)
            _pos++;

        string text = _text.Substring(start, _pos - start);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            throw Error($"숫자 형식이 올바르지 않습니다: {text}");
        return new JsonNumber(text);
    }

    private JsonNode ReadWord(string word, JsonNode value)
    {
        if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
            throw Error($"알 수 없는 값입니다 ('{word}'을(를) 기대함)");
        _pos += word.Length;
        return value;
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length)
        {
            char c = _text[_pos];
            if (c == '\n')
            {
                _pos++;
                _line++;
                _lineStart = _pos;
            }
            else if (c is ' ' or '\t' or '\r')
            {
                _pos++;
            }
            else if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
            {
                while (_pos < _text.Length && _text[_pos] != '\n')
                    _pos++;
            }
            else if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '*')
            {
                int end = _text.IndexOf("*/", _pos + 2, StringComparison.Ordinal);
                if (end < 0)
                    throw Error("주석(/* */)이 닫히지 않았습니다");
                for (int i = _pos; i < end; i++)
                {
                    if (_text[i] == '\n')
                    {
                        _line++;
                        _lineStart = i + 1;
                    }
                }

                _pos = end + 2;
            }
            else
            {
                break;
            }
        }
    }

    private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

    private void Expect(char c)
    {
        if (Peek() != c)
            throw Error($"'{c}'가 있어야 합니다");
        _pos++;
    }

    private LayoutFormatException Error(string message) =>
        new($"{message} ({_line}번째 줄, {_pos - _lineStart + 1}번째 글자)", "", _line);
}

/// <summary>JSON 값을 보기 좋게 씁니다 (들여쓰기 2칸, 짧은 객체·배열은 한 줄).</summary>
internal static class JsonWriter
{
    private const int InlineLimit = 72;

    public static string Write(JsonNode node)
    {
        var sb = new StringBuilder();
        WriteNode(sb, node, 0);
        sb.Append('\n');
        return sb.ToString();
    }

    private static void WriteNode(StringBuilder sb, JsonNode node, int indent)
    {
        switch (node)
        {
            case JsonObject obj when obj.Members.Count == 0:
                sb.Append("{}");
                break;
            case JsonArray array when array.Items.Count == 0:
                sb.Append("[]");
                break;
            case JsonObject or JsonArray when TryInline(node, out string inline) && indent * 2 + inline.Length <= InlineLimit:
                sb.Append(inline);
                break;
            case JsonObject obj:
                sb.Append("{\n");
                for (int i = 0; i < obj.Members.Count; i++)
                {
                    Indent(sb, indent + 1);
                    WriteString(sb, obj.Members[i].Key);
                    sb.Append(": ");
                    WriteNode(sb, obj.Members[i].Value, indent + 1);
                    sb.Append(i < obj.Members.Count - 1 ? ",\n" : "\n");
                }

                Indent(sb, indent);
                sb.Append('}');
                break;
            case JsonArray array:
                sb.Append("[\n");
                for (int i = 0; i < array.Items.Count; i++)
                {
                    Indent(sb, indent + 1);
                    WriteNode(sb, array.Items[i], indent + 1);
                    sb.Append(i < array.Items.Count - 1 ? ",\n" : "\n");
                }

                Indent(sb, indent);
                sb.Append(']');
                break;
            default:
                WriteScalar(sb, node);
                break;
        }
    }

    /// <summary>안에 객체·배열이 없으면 한 줄로 씁니다.</summary>
    private static bool TryInline(JsonNode node, out string text)
    {
        var sb = new StringBuilder();
        if (node is JsonObject obj)
        {
            sb.Append("{ ");
            for (int i = 0; i < obj.Members.Count; i++)
            {
                if (obj.Members[i].Value is JsonObject or JsonArray)
                {
                    text = "";
                    return false;
                }

                if (i > 0)
                    sb.Append(", ");
                WriteString(sb, obj.Members[i].Key);
                sb.Append(": ");
                WriteScalar(sb, obj.Members[i].Value);
            }

            sb.Append(" }");
        }
        else
        {
            var array = (JsonArray)node;
            sb.Append('[');
            for (int i = 0; i < array.Items.Count; i++)
            {
                if (array.Items[i] is JsonObject or JsonArray)
                {
                    text = "";
                    return false;
                }

                if (i > 0)
                    sb.Append(", ");
                WriteScalar(sb, array.Items[i]);
            }

            sb.Append(']');
        }

        text = sb.ToString();
        return true;
    }

    private static void WriteScalar(StringBuilder sb, JsonNode node)
    {
        switch (node)
        {
            case JsonNull:
                sb.Append("null");
                break;
            case JsonBool b:
                sb.Append(b.Value ? "true" : "false");
                break;
            case JsonNumber n:
                sb.Append(n.Text);
                break;
            case JsonString s:
                WriteString(sb, s.Value);
                break;
        }
    }

    private static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }

    private static void Indent(StringBuilder sb, int indent) => sb.Append(' ', indent * 2);
}
