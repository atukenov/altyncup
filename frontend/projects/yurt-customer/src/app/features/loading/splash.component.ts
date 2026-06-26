import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-splash',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-gradient-to-br from-amber-500 to-amber-600 transition-opacity duration-500"
      [class.opacity-0]="ready()"
      [class.pointer-events-none]="ready()"
    >
      <div class="flex flex-col items-center gap-6">
        <img src="logo.png" alt="Altyncup" class="w-28 h-28 object-contain drop-shadow-2xl splash-logo" />
        <div class="flex gap-1.5">
          <span class="w-2 h-2 rounded-full bg-white/60 splash-dot" style="animation-delay: 0s"></span>
          <span class="w-2 h-2 rounded-full bg-white/60 splash-dot" style="animation-delay: 0.2s"></span>
          <span class="w-2 h-2 rounded-full bg-white/60 splash-dot" style="animation-delay: 0.4s"></span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .splash-logo {
      animation: splash-pulse 1.2s ease-in-out infinite;
    }
    @keyframes splash-pulse {
      0%, 100% { transform: scale(1); opacity: 1; }
      50% { transform: scale(1.06); opacity: 0.9; }
    }
    .splash-dot {
      animation: splash-bounce 0.8s ease-in-out infinite;
    }
    @keyframes splash-bounce {
      0%, 100% { transform: translateY(0); opacity: 0.6; }
      50% { transform: translateY(-6px); opacity: 1; }
    }
  `],
})
export class SplashComponent {
  readonly ready = input.required<boolean>();
}
