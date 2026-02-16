#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Shortcutsy.Entities;

/// <summary>
/// Represents a missile fired from the player's launcher toward an enemy spaceship.
/// </summary>
public class Missile
{
    public Vector3 Position { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public bool IsActive { get; set; } = true;
    public Asteroid? TargetAsteroid { get; set; }
    public List<Vector3> Trail { get; set; } = new();

    public void Update(float deltaTime)
    {
        Trail.Add(Position);
        if (Trail.Count > 20) Trail.RemoveAt(0);

        Vector3 direction = TargetPosition - Position;
        float distance = direction.Length();
        direction.Normalize();

        if (distance < 20f)
        {
            IsActive = false;
            if (TargetAsteroid != null)
            {
                TargetAsteroid.IsActive = false;
            }
            return;
        }

        Velocity = direction * 800f;
        Position += Velocity * deltaTime;
    }
}
