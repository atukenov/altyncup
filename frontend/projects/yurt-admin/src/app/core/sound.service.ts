import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SoundService {
  private ctx: AudioContext | null = null;
  private unlocked = false;

  constructor() {
    const unlock = () => {
      if (this.unlocked) return;
      this.unlocked = true;
      this.getCtxAsync().catch(() => {});
      document.removeEventListener('click', unlock, true);
      document.removeEventListener('keydown', unlock, true);
    };
    document.addEventListener('click', unlock, true);
    document.addEventListener('keydown', unlock, true);
  }

  private async getCtxAsync(): Promise<AudioContext> {
    if (!this.ctx) this.ctx = new AudioContext();
    if (this.ctx.state === 'suspended') await this.ctx.resume();
    return this.ctx;
  }

  async playNewOrder(): Promise<void> {
    try {
      const ctx = await this.getCtxAsync();
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
