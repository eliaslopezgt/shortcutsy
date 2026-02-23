// Vector3 helper - lightweight replacement for XNA Vector3
export class Vec3 {
  constructor(public x: number = 0, public y: number = 0, public z: number = 0) {}

  add(other: Vec3): Vec3 {
    return new Vec3(this.x + other.x, this.y + other.y, this.z + other.z);
  }

  sub(other: Vec3): Vec3 {
    return new Vec3(this.x - other.x, this.y - other.y, this.z - other.z);
  }

  scale(s: number): Vec3 {
    return new Vec3(this.x * s, this.y * s, this.z * s);
  }

  length(): number {
    return Math.sqrt(this.x * this.x + this.y * this.y + this.z * this.z);
  }

  normalize(): Vec3 {
    const len = this.length();
    if (len === 0) return new Vec3();
    return new Vec3(this.x / len, this.y / len, this.z / len);
  }

  clone(): Vec3 {
    return new Vec3(this.x, this.y, this.z);
  }
}

// Color helper
export class Color {
  constructor(
    public r: number = 0,
    public g: number = 0,
    public b: number = 0,
    public a: number = 255
  ) {}

  toRgba(): string {
    return `rgba(${this.r}, ${this.g}, ${this.b}, ${this.a / 255})`;
  }

  toRgb(): string {
    return `rgb(${this.r}, ${this.g}, ${this.b})`;
  }

  withAlpha(a: number): Color {
    return new Color(this.r, this.g, this.b, a);
  }

  static readonly White = new Color(255, 255, 255);
  static readonly Black = new Color(0, 0, 0);
  static readonly Transparent = new Color(0, 0, 0, 0);
  static readonly Red = new Color(255, 0, 0);
  static readonly Yellow = new Color(255, 255, 0);
  static readonly Cyan = new Color(0, 255, 255);
  static readonly Gray = new Color(128, 128, 128);

  static fromFloat(r: number, g: number, b: number, a: number = 1): Color {
    return new Color(
      Math.min(255, Math.max(0, Math.round(r * 255))),
      Math.min(255, Math.max(0, Math.round(g * 255))),
      Math.min(255, Math.max(0, Math.round(b * 255))),
      Math.min(255, Math.max(0, Math.round(a * 255)))
    );
  }
}

// Game state enum
export enum GameState {
  Title,
  LevelSelect,
  Playing,
  Paused,
  GameOver,
  LevelTransition,
  NewRecord,
}
