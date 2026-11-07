using Godot;
using CoreColor = Tactile.Core.Primitives.Color;

namespace Tactile.Adapters.Godot.Utils;

public static class ColorExtensions
{
    public static Color ToGodot(this CoreColor c) => new(c.R, c.G, c.B, c.A);
}