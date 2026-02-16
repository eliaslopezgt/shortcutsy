using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;

namespace Shortcutsy.Entities;

/// <summary>
/// Represents an explosion effect with particles.
/// </summary>
public class Explosion
{
    public Vector3 Position { get; set; }
    public List<ExplosionParticle> Particles { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public float Time { get; set; }
    public float MaxTime { get; set; } = 1f;
    public float Size { get; set; } = 50f;

    public void Update(float deltaTime)
    {
        Time += deltaTime;
        if (Time >= MaxTime)
        {
            IsActive = false;
            return;
        }

        foreach (var particle in Particles)
        {
            particle.Position += particle.Velocity * deltaTime;
            particle.Velocity *= 0.95f;
            particle.Life -= deltaTime;
        }
        Particles.RemoveAll(p => p.Life <= 0);
    }
}

/// <summary>
/// A single particle within an explosion effect.
/// </summary>
public class ExplosionParticle
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Life { get; set; }
    public float MaxLife { get; set; }
    public Color Color { get; set; }
    public float Size { get; set; }
}
