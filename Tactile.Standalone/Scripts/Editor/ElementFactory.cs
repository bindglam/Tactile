using System;
using System.Collections.Generic;
using System.Linq;
using Tactile.Core.Elements;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Layout.Serialization;
using Tactile.Core.Primitives;

namespace Tactile.Standalone.Editor;

/// <summary>새 요소 만들기, 복제, 고유 ID.</summary>
public static class ElementFactory
{
    /// <summary>추가할 수 있는 요소 종류 (등록표에 있고 매개변수 없는 생성자가 있는 것). 기본 요소가 먼저.</summary>
    public static IReadOnlyList<Type> CreatableTypes(ElementRegistry registry)
    {
        var builtin = new[] { typeof(KeyElement), typeof(StatElement), typeof(HandsElement) };
        var types = new List<Type>();
        foreach (string id in registry.TypeIds)
        {
            if (registry.TryGetElementType(id, out Type type) && type.GetConstructor(Type.EmptyTypes) is not null)
                types.Add(type);
        }

        return types.OrderBy(t => Array.IndexOf(builtin, t) is int i && i >= 0 ? i : int.MaxValue).ThenBy(t => t.Name).ToList();
    }

    /// <summary>새 요소를 캔버스 가운데에 만듭니다. 이미 같은 종류가 있으면 모양은 그것을 따릅니다.</summary>
    public static LayoutElement Create(Type type, KeyViewerLayout layout)
    {
        var element = (LayoutElement)Activator.CreateInstance(type)!;
        var size = new Vector2(100, 100);

        switch (element)
        {
            case KeyElement key:
                size = new Vector2(80, 80);
                key.Label = "Key";
                if (layout.Elements.OfType<KeyElement>().LastOrDefault() is { } existing)
                {
                    key.Style = existing.Style.Clone();
                    key.Rain = existing.Rain;
                    key.ShowCount = existing.ShowCount;
                    size = existing.Bounds.Size;
                }

                break;

            case StatElement stat:
                size = new Vector2(160, 36);
                stat.Kind = StatKind.Kps;
                stat.Format = "KPS {0:0}";
                if (layout.Elements.OfType<StatElement>().LastOrDefault() is { } existingStat)
                {
                    stat.Look = existingStat.Look;
                    size = existingStat.Bounds.Size;
                }

                break;

            case HandsElement:
                size = new Vector2(400, 220);
                break;
        }

        float x = (float)Math.Round((layout.Canvas.X - size.X) / 2f);
        float y = (float)Math.Round((layout.Canvas.Y - size.Y) / 2f);
        element.Bounds = new Rect(Math.Max(0, x), Math.Max(0, y), size.X, size.Y);
        element.Id = UniqueId(layout, Prefix(type));
        return element;
    }

    /// <summary>요소를 복제합니다 (조금 옆으로, 새 ID).</summary>
    public static LayoutElement Duplicate(LayoutElement element, KeyViewerLayout layout)
    {
        // 레이아웃 파일 왕복으로 깊은 복사 (애드온 요소, 알 수 없는 요소 포함)
        var temp = new KeyViewerLayout { Elements = { element } };
        LayoutElement copy = LayoutSerializer.Clone(temp).Elements[0];
        copy.Bounds = new Rect(element.Bounds.X + 10, element.Bounds.Y + 10, element.Bounds.Width, element.Bounds.Height);
        copy.Id = UniqueId(layout, Prefix(element.GetType()));
        return copy;
    }

    public static string UniqueId(KeyViewerLayout layout, string prefix)
    {
        var used = new HashSet<string>(layout.Elements.Select(e => e.Id), StringComparer.Ordinal);
        for (int n = 1; ; n++)
        {
            string id = $"{prefix}-{n}";
            if (!used.Contains(id))
                return id;
        }
    }

    private static string Prefix(Type type)
    {
        string name = type.Name.EndsWith("Element", StringComparison.Ordinal) ? type.Name.Substring(0, type.Name.Length - 7) : type.Name;
        return name.ToLowerInvariant();
    }
}
