using UnityEngine;
using CoreColor = Tactile.Core.Primitives.Color;

namespace Tactile.Adapters.Unity.Utils;

internal static class ColorExtensions
{
    public static Color ToUnity(this CoreColor color) => new(color.R, color.G, color.B, color.A);

    public static Color32 ToUnity32(this CoreColor color) => color.ToUnity();
}
