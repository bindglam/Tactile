using System.Runtime.CompilerServices;
using Godot;
using Tactile.Core.Layout;

namespace Tactile.Adapters.Godot;

/// <summary>
/// 레이아웃 리소스(<see cref="LayoutAsset"/>) → 텍스처 캐시. 같은 리소스 객체는 한 번만 디코딩합니다.
/// <para>
/// 리소스 객체가 더 이상 쓰이지 않으면 텍스처도 함께 놓아 줍니다 (약한 참조).
/// 레이아웃을 자주 다시 만드는 경우(에디터 미리보기 등) 하나를 만들어 <see cref="TactileGodotOptions.AssetTextures"/>로 공유하세요.
/// </para>
/// </summary>
public sealed class GodotAssetTextures
{
    private readonly ConditionalWeakTable<LayoutAsset, Entry> _cache = new();

    /// <summary>리소스의 텍스처. 이미지가 아니거나 읽을 수 없으면 null (오류는 한 번만 출력).</summary>
    public Texture2D? Get(LayoutAsset? asset)
    {
        if (asset is null || !asset.IsImage)
            return null;
        return _cache.GetValue(asset, Load).Texture;
    }

    private static Entry Load(LayoutAsset asset)
    {
        string format = asset.Format.Length > 0 ? asset.Format : LayoutAsset.DetectImageFormat(asset.Data) ?? "";
        var image = new Image();
        Error error = format switch
        {
            "png" => image.LoadPngFromBuffer(asset.Data),
            "jpg" or "jpeg" => image.LoadJpgFromBuffer(asset.Data),
            "webp" => image.LoadWebpFromBuffer(asset.Data),
            "bmp" => image.LoadBmpFromBuffer(asset.Data),
            "tga" => image.LoadTgaFromBuffer(asset.Data),
            "svg" => image.LoadSvgFromBuffer(asset.Data),
            _ => Error.FileUnrecognized,
        };

        if (error != Error.Ok || image.IsEmpty())
        {
            GD.PushWarning($"[Tactile] '{asset.Id}' 이미지를 읽지 못했습니다 (형식: {(format.Length > 0 ? format : "알 수 없음")}, {error}).");
            return new Entry(null);
        }

        return new Entry(ImageTexture.CreateFromImage(image));
    }

    private sealed class Entry(Texture2D? texture)
    {
        public Texture2D? Texture { get; } = texture;
    }
}
