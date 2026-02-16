using Microsoft.Xna.Framework;

namespace Shortcutsy.Entities;

/// <summary>
/// Represents a background star in the scrolling starfield effect.
/// </summary>
public class Star
{
    public Vector3 Position;
    public float Size { get; set; }
    public float Brightness { get; set; }
    public float TwinkleSpeed { get; set; }
    public float TwinkleOffset { get; set; }
}
