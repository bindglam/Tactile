using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Serialization;

namespace Tactile.Standalone.Editor;

/// <summary>
/// 레이아웃 리소스 편집 도우미: 가져오기, 이름 바꾸기, 지우기, 쓰는 곳 세기.
/// 요소 안의 리소스 참조는 <see cref="AssetReferenceAttribute"/>가 붙은 string 속성으로 찾습니다 (애드온 요소 포함).
/// </summary>
public static class AssetTools
{
    /// <summary>가져올 수 있는 이미지 확장자.</summary>
    public static readonly string[] ImageExtensions = { "png", "jpg", "jpeg", "webp", "bmp", "svg" };

    /// <summary>얼불춤 모드(Unity)가 읽을 수 있는 이미지 형식.</summary>
    public static bool IsPortableImage(LayoutAsset asset) => asset.Format is "png" or "jpg" or "jpeg";

    private const int MaxBytes = 16 * 1024 * 1024;

    public static bool IsImageFile(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path).TrimStart('.').ToLowerInvariant());

    /// <summary>이미지 파일들을 리소스로 추가합니다. 추가한 리소스와 실패 사유를 돌려줍니다 (되돌리기 단계는 호출하는 쪽에서).</summary>
    public static (List<LayoutAsset> Added, List<string> Errors) ImportImages(KeyViewerLayout layout, IEnumerable<string> paths)
    {
        var added = new List<LayoutAsset>();
        var errors = new List<string>();
        foreach (string path in paths)
        {
            string name = Path.GetFileName(path);
            if (!IsImageFile(path))
            {
                errors.Add($"{name}: 이미지 파일이 아닙니다 ({string.Join(", ", ImageExtensions)})");
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Length > MaxBytes)
                {
                    errors.Add($"{name}: 너무 큽니다 ({info.Length / 1024 / 1024}MB, 최대 {MaxBytes / 1024 / 1024}MB)");
                    continue;
                }

                byte[] data = File.ReadAllBytes(path);
                string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                LayoutAsset asset = LayoutAsset.Image(UniqueId(layout, Path.GetFileNameWithoutExtension(path)), data, extension);
                if (asset.Format == "jpeg")
                    asset.Format = "jpg";
                layout.Assets.Add(asset);
                added.Add(asset);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{name}: {e.Message}");
            }
        }

        return (added, errors);
    }

    /// <summary>겹치지 않는 리소스 이름. 공백은 '-'로 바꿉니다.</summary>
    public static string UniqueId(KeyViewerLayout layout, string wanted)
    {
        string baseId = string.Join("-", wanted.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (baseId.Length == 0)
            baseId = "image";
        string id = baseId;
        for (int n = 2; layout.FindAsset(id) is not null; n++)
            id = $"{baseId}-{n}";
        return id;
    }

    /// <summary>리소스 이름을 바꾸고 쓰는 곳도 함께 고칩니다. 이름이 비었거나 겹치면 false.</summary>
    public static bool Rename(KeyViewerLayout layout, LayoutAsset asset, string newId)
    {
        newId = newId.Trim();
        int index = layout.Assets.IndexOf(asset);
        if (index < 0 || newId.Length == 0 || newId == asset.Id || layout.FindAsset(newId) is not null)
            return false;

        string oldId = asset.Id;
        layout.Assets[index] = asset.WithId(newId); // 리소스 객체는 되돌리기 기록과 공유하므로 바꾸지 않고 교체
        RewriteReferences(layout, asset.Type, id => id == oldId ? newId : id);
        return true;
    }

    /// <summary>리소스를 지우고, 쓰던 곳은 비웁니다 (단색으로 돌아감).</summary>
    public static void Remove(KeyViewerLayout layout, LayoutAsset asset)
    {
        if (!layout.Assets.Remove(asset))
            return;
        if (layout.FindAsset(asset.Id) is null)
            RewriteReferences(layout, asset.Type, id => id == asset.Id ? null : id);
    }

    /// <summary>리소스를 가리키는 요소 속성 수.</summary>
    public static int CountUses(KeyViewerLayout layout, LayoutAsset asset)
    {
        int count = 0;
        RewriteReferences(layout, asset.Type, id =>
        {
            if (id == asset.Id)
                count++;
            return id;
        });
        return count;
    }

    // ---- 참조 찾기 (리플렉션) ----

    private static void RewriteReferences(KeyViewerLayout layout, string assetType, Func<string?, string?> map)
    {
        foreach (object element in layout.Elements)
            Rewrite(element, assetType, map, 0);
    }

    /// <summary>객체 안의 리소스 참조를 바꿉니다. 구조체는 복사본이므로 바뀐 값을 돌려받아 다시 넣습니다.</summary>
    private static object Rewrite(object target, string assetType, Func<string?, string?> map, int depth)
    {
        if (depth > 16)
            return target; // 순환 참조 방지

        foreach (PropertyInfo property in Properties(target.GetType()))
        {
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type == typeof(string))
            {
                if (property.GetCustomAttribute<AssetReferenceAttribute>() is { } reference && reference.AssetType == assetType)
                {
                    var current = (string?)property.GetValue(target);
                    string? next = map(current);
                    if (next != current)
                        property.SetValue(target, next);
                }

                continue;
            }

            if (!IsWalkable(type) || property.GetValue(target) is not { } value)
                continue;

            if (value is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is { } item && IsWalkable(item.GetType()))
                    {
                        object result = Rewrite(item, assetType, map, depth + 1);
                        if (item.GetType().IsValueType)
                            list[i] = result; // 구조체 항목은 복사본이므로 다시 넣음 (배열도 항목 교체는 가능)
                    }
                }

                continue;
            }

            object rewritten = Rewrite(value, assetType, map, depth + 1);
            if (type.IsValueType)
                property.SetValue(target, rewritten);
        }

        return target;
    }

    private static bool IsWalkable(Type type) =>
        !type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(byte[]) && type != typeof(decimal)
        && type.Namespace != "Tactile.Core.Primitives";

    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();

    private static PropertyInfo[] Properties(Type type)
    {
        if (PropertyCache.TryGetValue(type, out PropertyInfo[]? cached))
            return cached;
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetSetMethod(false) is not null && p.GetIndexParameters().Length == 0
                        && !p.IsDefined(typeof(LayoutIgnoreAttribute), true))
            .ToArray();
        PropertyCache[type] = properties;
        return properties;
    }
}
