/**
 * Audio manager using Web Audio API.
 * Procedurally generates explosion and shoot sounds, matching the C# version.
 */
export class AudioManager {
  private ctx: AudioContext | null = null;
  private _muted: boolean = false;

  get muted(): boolean { return this._muted; }

  private ensureContext(): AudioContext {
    if (!this.ctx) {
      this.ctx = new AudioContext();
    }
    if (this.ctx.state === 'suspended') {
      this.ctx.resume();
    }
    return this.ctx;
  }

  toggleMute(): void {
    this._muted = !this._muted;
  }

  playExplosion(): void {
    if (this._muted) return;
    const ctx = this.ensureContext();

    const sampleRate = ctx.sampleRate;
    const duration = 0.2; // 200ms
    const samples = Math.floor(sampleRate * duration);
    const buffer = ctx.createBuffer(1, samples, sampleRate);
    const data = buffer.getChannelData(0);

    for (let i = 0; i < samples; i++) {
      const t = i / sampleRate;
      const freq = 100 * Math.exp(-t * 10);
      data[i] = Math.sin(2 * Math.PI * freq * t) * 0.3 * Math.exp(-t * 8);
    }

    const source = ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(ctx.destination);
    source.start();
  }

  playShoot(): void {
    if (this._muted) return;
    const ctx = this.ensureContext();

    const sampleRate = ctx.sampleRate;
    const duration = 0.1; // 100ms
    const samples = Math.floor(sampleRate * duration);
    const buffer = ctx.createBuffer(1, samples, sampleRate);
    const data = buffer.getChannelData(0);

    for (let i = 0; i < samples; i++) {
      const t = i / sampleRate;
      const freq = 800 - t * 3000;
      data[i] = Math.sin(2 * Math.PI * freq * t) * 0.2 * Math.exp(-t * 20);
    }

    const source = ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(ctx.destination);
    source.start();
  }
}
