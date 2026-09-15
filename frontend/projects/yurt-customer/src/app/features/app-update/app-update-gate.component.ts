import { Component, inject } from '@angular/core';
import { AppUpdateService } from '../../core/app-update.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-update-gate',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    @if (svc.updateRequired()) {
      <div class="fixed inset-0 z-9999 bg-black/70 flex items-center justify-center p-6" role="dialog" aria-modal="true" aria-labelledby="update-gate-title">
        <div class="bg-white rounded-3xl shadow-2xl w-full max-w-sm p-8 text-center">
          <div class="w-16 h-16 rounded-2xl bg-amber-100 flex items-center justify-center text-3xl mx-auto mb-4" aria-hidden="true">⬆️</div>
          <h2 id="update-gate-title" class="text-xl font-bold text-stone-800 mb-2">{{ 'update.title' | t }}</h2>
          <p class="text-stone-500 text-sm mb-6">{{ 'update.body' | t }}</p>
          <a
            [href]="svc.storeUrl()"
            class="block w-full bg-amber-500 active:bg-amber-600 text-stone-900 font-semibold rounded-2xl px-5 py-3 text-sm transition-colors"
          >
            {{ 'update.cta' | t }}
          </a>
        </div>
      </div>
    }
  `,
})
export class AppUpdateGateComponent {
  readonly svc = inject(AppUpdateService);
}
