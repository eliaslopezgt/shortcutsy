/**
 * Audio manager using Web Audio API.
 * Plays explosion, shoot sounds, and background music from WAV file.
 */
export class AudioManager {
  private ctx: AudioContext | null = null;
  private _muted: boolean = false;
  private musicSource: AudioBufferSourceNode | null = null;
  private musicBuffer: AudioBuffer | null = null;
  private musicGain: GainNode | null = null;

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
    if (this._muted) {
      this.stopMusic();
    } else if (this.musicBuffer) {
      this.startMusic();
    }
  }

  async loadMusic(): Promise<void> {
    try {
      const response = await fetch('/music.wav');
      const arrayBuffer = await response.arrayBuffer();
      const ctx = this.ensureContext();
      this.musicBuffer = await ctx.decodeAudioData(arrayBuffer);
    } catch (e) {
      console.warn('Could not load music:', e);
    }
  }

  startMusic(): void {
    if (this._muted || !this.musicBuffer) return;
    this.stopMusic();

    const ctx = this.ensureContext();
    this.musicGain = ctx.createGain();
    this.musicGain.gain.value = 0.5;
    this.musicGain.connect(ctx.destination);

    this.musicSource = ctx.createBufferSource();
    this.musicSource.buffer = this.musicBuffer;
    this.musicSource.loop = true;
    this.musicSource.connect(this.musicGain);
    this.musicSource.start();
  }

  stopMusic(): void {
    if (this.musicSource) {
      try { this.musicSource.stop(); } catch {}
      this.musicSource = null;
    }
    this.musicGain = null;
  }

  playExplosion(): void {
    if (this._muted) return;
    const ctx = this.ensureContext();

    const sampleRate = ctx.sampleRate;
    const duration = 0.2;
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
    const duration = 0.1;
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
