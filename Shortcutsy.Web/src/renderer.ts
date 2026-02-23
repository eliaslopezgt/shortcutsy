/**
 * Canvas 2D renderer - replaces SpriteBatch + TextRenderer from MonoGame.
 * All drawing goes through this to keep the game code clean.
 */
export class Renderer {
  private ctx: CanvasRenderingContext2D;
  readonly width = 640;
  readonly height = 480;

  constructor(private canvas: HTMLCanvasElement) {
    this.ctx = canvas.getContext('2d')!;
    canvas.width = this.width;
    canvas.height = this.height;
  }

  clear(r: number = 5, g: number = 5, b: number = 16): void {
    this.ctx.fillStyle = `rgb(${r},${g},${b})`;
    this.ctx.fillRect(0, 0, this.width, this.height);
  }

  /** Save/restore transform for screen shake */
  save(): void { this.ctx.save(); }
  restore(): void { this.ctx.restore(); }
  translate(x: number, y: number): void { this.ctx.translate(x, y); }

  /** Set global composite operation (for additive blending) */
  setBlendMode(mode: GlobalCompositeOperation): void {
    this.ctx.globalCompositeOperation = mode;
  }

  /** Draw a filled rectangle */
  fillRect(x: number, y: number, w: number, h: number, r: number, g: number, b: number, a: number = 255): void {
    this.ctx.fillStyle = `rgba(${r},${g},${b},${a / 255})`;
    this.ctx.fillRect(Math.round(x), Math.round(y), Math.round(w), Math.round(h));
  }

  /** Draw a single pixel */
  drawPixel(x: number, y: number, r: number, g: number, b: number, a: number = 255): void {
    this.ctx.fillStyle = `rgba(${r},${g},${b},${a / 255})`;
    this.ctx.fillRect(Math.round(x), Math.round(y), 1, 1);
  }

  /** Measure text width */
  measureText(text: string, fontSize: number = 16, fontFamily: string = 'Consolas, monospace'): { width: number; height: number } {
    this.ctx.font = `bold ${fontSize}px ${fontFamily}`;
    const m = this.ctx.measureText(text);
    return { width: m.width, height: fontSize * 1.2 };
  }

  /** Draw text */
  drawText(
    text: string,
    x: number,
    y: number,
    r: number,
    g: number,
    b: number,
    a: number = 255,
    fontSize: number = 16,
    fontFamily: string = 'Consolas, monospace',
    scale: number = 1
  ): void {
    if (!text) return;
    this.ctx.save();
    this.ctx.font = `bold ${fontSize}px ${fontFamily}`;
    this.ctx.fillStyle = `rgba(${r},${g},${b},${a / 255})`;
    if (scale !== 1) {
      this.ctx.translate(x, y);
      this.ctx.scale(scale, scale);
      this.ctx.fillText(text, 0, fontSize * 0.85);
      this.ctx.restore();
      return;
    }
    this.ctx.fillText(text, Math.round(x), Math.round(y + fontSize * 0.85));
    this.ctx.restore();
  }

  /** Draw text with black outline (shadow) for readability */
  drawTextOutlined(
    text: string,
    x: number,
    y: number,
    r: number,
    g: number,
    b: number,
    a: number = 255,
    fontSize: number = 16,
    fontFamily: string = 'Consolas, monospace',
    scale: number = 1,
    outlineWidth: number = 1
  ): void {
    // Draw black outline
    for (let ox = -outlineWidth; ox <= outlineWidth; ox++) {
      for (let oy = -outlineWidth; oy <= outlineWidth; oy++) {
        if (ox === 0 && oy === 0) continue;
        this.drawText(text, x + ox, y + oy, 0, 0, 0, a, fontSize, fontFamily, scale);
      }
    }
    // Draw main text
    this.drawText(text, x, y, r, g, b, a, fontSize, fontFamily, scale);
  }

  /** Helper: measure text with "normal" font (16px Consolas bold) */
  measureNormal(text: string): { width: number; height: number } {
    return this.measureText(text, 16, 'Consolas, monospace');
  }

  /** Helper: measure text with "large" font (28px Consolas bold) */
  measureLarge(text: string): { width: number; height: number } {
    return this.measureText(text, 28, 'Consolas, monospace');
  }

  /** Helper: measure text with "huge" font (52px Impact bold) */
  measureHuge(text: string): { width: number; height: number } {
    return this.measureText(text, 52, 'Impact, sans-serif');
  }

  /** Helper: measure text with "small" font (10px Consolas bold) */
  measureSmall(text: string): { width: number; height: number } {
    return this.measureText(text, 10, 'Consolas, monospace');
  }

  drawNormal(text: string, x: number, y: number, r: number, g: number, b: number, a: number = 255, scale: number = 1): void {
    this.drawText(text, x, y, r, g, b, a, 16, 'Consolas, monospace', scale);
  }

  drawLarge(text: string, x: number, y: number, r: number, g: number, b: number, a: number = 255): void {
    this.drawText(text, x, y, r, g, b, a, 28, 'Consolas, monospace');
  }

  drawHuge(text: string, x: number, y: number, r: number, g: number, b: number, a: number = 255): void {
    this.drawText(text, x, y, r, g, b, a, 52, 'Impact, sans-serif');
  }

  drawSmall(text: string, x: number, y: number, r: number, g: number, b: number, a: number = 255): void {
    this.drawText(text, x, y, r, g, b, a, 10, 'Consolas, monospace');
  }

  /** Draw a circle (for medals, etc.) */
  fillCircle(cx: number, cy: number, radius: number, r: number, g: number, b: number, a: number = 255): void {
    this.ctx.beginPath();
    this.ctx.arc(cx, cy, radius, 0, Math.PI * 2);
    this.ctx.fillStyle = `rgba(${r},${g},${b},${a / 255})`;
    this.ctx.fill();
  }

  /** Draw a ring (circle outline) */
  strokeCircle(cx: number, cy: number, radius: number, lineWidth: number, r: number, g: number, b: number, a: number = 255): void {
    this.ctx.beginPath();
    this.ctx.arc(cx, cy, radius, 0, Math.PI * 2);
    this.ctx.strokeStyle = `rgba(${r},${g},${b},${a / 255})`;
    this.ctx.lineWidth = lineWidth;
    this.ctx.stroke();
  }
}
