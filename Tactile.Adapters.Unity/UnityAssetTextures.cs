using Tactile.Core.Layout;
using UnityEngine;

namespace Tactile.Adapters.Unity;

/// <summary>
/// 레이아웃 리소스(<see cref="LayoutAsset"/>) → 텍스처 캐시. 같은 리소스 객체는 한 번만 디코딩하며,
/// 해제하면 만든 텍스처를 모두 지웁니다. Unity가 읽을 수 있는 이미지는 PNG와 JPG입니다.
/// </summary>
public sealed class UnityAssetTextures : IDisposable
{
    private readonly Dictionary<LayoutAsset, Texture2D?> _cache = new();

    /// <summary>리소스의 텍스처. 이미지가 아니거나 읽을 수 없으면 null (오류는 한 번만 출력).</summary>
    public Texture2D? Get(LayoutAsset? asset)
    {
        if (asset is null || !asset.IsImage)
            return null;
        if (!_cache.TryGetValue(asset, out Texture2D? texture))
        {
            texture = Load(asset);
            _cache.Add(asset, texture);
        }

        return texture;
    }

    public void Dispose()
    {
        foreach (Texture2D? texture in _cache.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _cache.Clear();
    }

    private static Texture2D? Load(LayoutAsset asset)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
        {
            name = $"Tactile_{asset.Id}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        // PNG/JPG만 읽을 수 있음. 실패하면 false
        if (ImageConversion.LoadImage(texture, asset.Data, markNonReadable: true))
            return texture;

        UnityEngine.Object.Destroy(texture);
        Debug.LogWarning($"[Tactile] '{asset.Id}' 이미지를 읽지 못했습니다 (형식: {asset.Format}). Unity는 PNG와 JPG만 읽을 수 있습니다.");
        return null;
    }
}
