import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SoundService {
  private ctx: AudioContext | null = null;

  private getCtx(): AudioContext {
    if (!this.ctx) this.ctx = new AudioContext();
    // Resume if suspended (browser autoplay policy)
    if (this.ctx.state === 'suspended') this.ctx.resume();
    return this.ctx;
  }

  playNewOrder(): void {
    try {
      const ctx = this.getCtx();
      // Two-tone chime: A5 then C#6
      this.tone(ctx, 880,  0,    0.18);
      this.tone(ctx, 1108, 0.2,  0.22);
    } catch {
      // Silently ignore if audio is blocked
    }
  }

  private tone(ctx: AudioContext, freq: number, delay: number, duration: number): void {
    const osc  = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.type = 'sine';
    osc.frequency.value = freq;
    const t = ctx.currentTime + delay;
    gain.gain.setValueAtTime(0.35, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + duration);
    osc.start(t);
    osc.stop(t + duration);
  }
}
