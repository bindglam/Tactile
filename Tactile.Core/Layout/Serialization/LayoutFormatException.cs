namespace Tactile.Core.Layout.Serialization;

/// <summary>레이아웃 파일의 형식이나 값이 올바르지 않을 때 던집니다.</summary>
public sealed class LayoutFormatException : FormatException
{
    internal LayoutFormatException(string message, string path, int line)
        : base(message)
    {
        Path = path;
        Line = line;
    }

    /// <summary>문제가 난 값의 위치. 예: <c>elements[2].style.normal.fill</c> (JSON 문법 오류면 빈 문자열).</summary>
    public string Path { get; }

    /// <summary>파일에서 문제가 난 줄 (1부터, 모르면 0).</summary>
    public int Line { get; }
}
