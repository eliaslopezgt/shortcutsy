#nullable enable
using Microsoft.Xna.Framework;
using Shortcutsy.Data;

namespace Shortcutsy.Entities;

/// <summary>
/// Represents an enemy spaceship that descends toward the player, displaying a keyboard shortcut.
/// The player must press the correct key combination to destroy it.
/// </summary>
public class Asteroid
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public ShortcutItem? Shortcut { get; set; }
    public int Size { get; set; } = 32;
    public bool IsActive { get; set; } = true;
    public bool ShowShortcut { get; set; }
    public float SpawnTime { get; set; }
    public int WrongAttempts { get; set; } = 0;

    public void Update(float deltaTime)
    {
        Position += Velocity * deltaTime;
    }

    /// <summary>
    /// Checks if the spaceship has reached the player's position.
    /// </summary>
    public bool HasReachedPlayer()
    {
        return Position.Z < -50f;
    }
}
