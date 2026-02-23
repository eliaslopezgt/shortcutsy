import { Vec3 } from './types';
import { ShortcutItem } from './shortcutItem';

// Star entity
export class Star {
  position: Vec3;
  size: number;
  brightness: number;
  twinkleSpeed: number;
  twinkleOffset: number;

  constructor() {
    this.position = new Vec3();
    this.size = 1;
    this.brightness = 1;
    this.twinkleSpeed = 1;
    this.twinkleOffset = 0;
  }
}

// Asteroid (enemy spaceship) entity
export class Asteroid {
  position: Vec3 = new Vec3();
  velocity: Vec3 = new Vec3();
  shortcut: ShortcutItem | null = null;
  size: number = 32;
  isActive: boolean = true;
  showShortcut: boolean = false;
  spawnTime: number = 0;
  wrongAttempts: number = 0;

  update(deltaTime: number): void {
    this.position = this.position.add(this.velocity.scale(deltaTime));
  }

  hasReachedPlayer(): boolean {
    return this.position.z < -50;
  }
}

// Missile entity
export class Missile {
  position: Vec3 = new Vec3();
  targetPosition: Vec3 = new Vec3();
  velocity: Vec3 = new Vec3();
  isActive: boolean = true;
  targetAsteroid: Asteroid | null = null;
  trail: Vec3[] = [];

  update(deltaTime: number): void {
    this.trail.push(this.position.clone());
    if (this.trail.length > 20) this.trail.shift();

    const direction = this.targetPosition.sub(this.position);
    const distance = direction.length();
    const normalized = direction.normalize();

    if (distance < 20) {
      this.isActive = false;
      if (this.targetAsteroid) {
        this.targetAsteroid.isActive = false;
      }
      return;
    }

    this.velocity = normalized.scale(800);
    this.position = this.position.add(this.velocity.scale(deltaTime));
  }
}

// Explosion particle
export class ExplosionParticle {
  position: Vec3 = new Vec3();
  velocity: Vec3 = new Vec3();
  life: number = 0;
  maxLife: number = 0;
  color: { r: number; g: number; b: number } = { r: 255, g: 100, b: 0 };
  size: number = 3;
}

// Explosion entity
export class Explosion {
  position: Vec3 = new Vec3();
  particles: ExplosionParticle[] = [];
  isActive: boolean = true;
  time: number = 0;
  maxTime: number = 1;
  size: number = 50;

  update(deltaTime: number): void {
    this.time += deltaTime;
    if (this.time >= this.maxTime) {
      this.isActive = false;
      return;
    }

    for (const p of this.particles) {
      p.position = p.position.add(p.velocity.scale(deltaTime));
      p.velocity = p.velocity.scale(0.95);
      p.life -= deltaTime;
    }
    this.particles = this.particles.filter(p => p.life > 0);
  }
}
