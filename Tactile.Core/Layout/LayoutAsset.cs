namespace Tactile.Core.Layout;

/// <summary>
/// 레이아웃 파일에 함께 들어가는 리소스 (이미지 등). 파일에는 <c>data</c>를 base64로 저장합니다.
/// <code>
/// { "id": "key-normal", "type": "image", "format": "png", "data": "iVBORw0..." }
/// </code>
/// <para>
/// 요소는 <see cref="Id"/>로 리소스를 가리킵니다 (예: <see cref="BoxStyle.SpriteKey"/>).
/// 여러 요소·키뷰어·되돌리기 기록이 같은 객체를 공유하므로, 만든 뒤에는 바꾸지 말고 새 객체로 교체하세요
/// (<see cref="WithId"/>).
/// </para>
/// </summary>
public sealed class LayoutAsset
{
    /// <summary>이미지 종류 (<see cref="Type"/>).</summary>
    public const string ImageType = "image";

    /// <summary>소리 종류 (<see cref="Type"/>).</summary>
    public const string SoundType = "sound";

    /// <summary>레이아웃 안에서 리소스를 가리키는 이름. 레이아웃 안에서 겹치지 않아야 합니다.</summary>
    public string Id { get; set; } = "";

    /// <summary>종류: <see cref="ImageType"/>, <see cref="SoundType"/> 등.</summary>
    public string Type { get; set; } = ImageType;

    /// <summary>파일 형식 (확장자, 소문자): <c>"png"</c>, <c>"jpg"</c>, <c>"webp"</c>, <c>"wav"</c> 등.</summary>
    public string Format { get; set; } = "";

    /// <summary>파일 내용 그대로.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public bool IsImage => Type == ImageType;

    /// <summary>이름만 바꾼 복사본 (데이터는 공유).</summary>
    public LayoutAsset WithId(string id) => new() { Id = id, Type = Type, Format = Format, Data = Data };

    /// <summary>이미지 파일 내용으로 리소스를 만듭니다. 형식은 내용에서 알아내고, 모르면 <paramref name="fallbackFormat"/>.</summary>
    public static LayoutAsset Image(string id, byte[] data, string fallbackFormat = "") => new()
    {
        Id = id,
        Type = ImageType,
        Format = DetectImageFormat(data) ?? fallbackFormat.TrimStart('.').ToLowerInvariant(),
        Data = data ?? throw new ArgumentNullException(nameof(data)),
    };

    /// <summary>파일 앞부분으로 이미지 형식을 알아냅니다: png, jpg, webp, bmp, gif. 모르면 null.</summary>
    public static string? DetectImageFormat(byte[] data)
    {
        if (data is null || data.Length < 12)
            return null;
        if (data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G')
            return "png";
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "jpg";
        if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            return "webp";
        if (data[0] == 'B' && data[1] == 'M')
            return "bmp";
        if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
            return "gif";
        return null;
    }
}
