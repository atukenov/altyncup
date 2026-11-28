import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/** Longest side of an uploaded photo. Enough for the item detail hero on a 3x phone. */
const MAX_SIDE = 1200;
/** Files already this small (and in a web format) are uploaded untouched. */
const KEEP_ORIGINAL_BYTES = 400 * 1024;
const WEB_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

/**
 * Image picker for admin forms: upload a photo (shrunk in the browser first) or paste a link.
 * Either way the value is a plain URL, so the customer apps keep rendering `imageUrl` as before.
 */
@Component({
  selector: 'admin-image-upload-field',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  template: `
    <div class="flex items-start gap-3">
      <div class="w-20 h-20 shrink-0 rounded-xl border border-slate-200 bg-slate-50 overflow-hidden flex items-center justify-center">
        @if (value() && !broken()) {
          <img [src]="value()" alt="" class="w-full h-full object-cover" (error)="broken.set(true)" (load)="broken.set(false)" />
        } @else {
          <span class="text-2xl text-slate-300" aria-hidden="true">🖼️</span>
        }
      </div>
      <div class="flex-1 min-w-0 space-y-2">
        <div class="flex flex-wrap items-center gap-2">
          <label
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-sm font-medium cursor-pointer bg-amber-100 text-amber-800 hover:bg-amber-200"
            [class.opacity-60]="uploading()"
            [class.pointer-events-none]="uploading()"
          >
            <input type="file" accept="image/*" class="sr-only" [disabled]="uploading()" (change)="onFile($event)" />
            {{ uploading() ? ('image.uploading' | t) : value() ? ('image.replace' | t) : ('image.upload' | t) }}
          </label>
          @if (value() && !uploading()) {
            <button type="button" class="text-sm text-slate-500 hover:text-red-600" (click)="setValue('')">
              {{ 'image.remove' | t }}
            </button>
          }
        </div>
        <input
          [id]="inputId()"
          type="text"
          [ngModel]="value()"
          (ngModelChange)="setValue($event)"
          [placeholder]="'image.orUrl' | t"
          class="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:ring-2 focus:ring-amber-400 outline-none"
        />
        @if (value() && broken()) {
          <p class="text-xs text-red-600">{{ 'image.broken' | t }}</p>
        } @else {
          <p class="text-xs text-slate-400">{{ 'image.hint' | t }}</p>
        }
      </div>
    </div>
  `,
})
export class ImageUploadFieldComponent {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  readonly value = input<string>('');
  readonly inputId = input<string>('image-url');
  readonly valueChange = output<string>();

  readonly uploading = signal(false);
  readonly broken = signal(false);

  setValue(url: string): void {
    this.broken.set(false);
    this.valueChange.emit((url ?? '').trim());
  }

  async onFile(event: Event): Promise<void> {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = ''; // let the same file be picked again after an error
    if (!file) return;

    this.uploading.set(true);
    try {
      const { blob, name } = await prepareImage(file);
      this.api.adminUploadImage(blob, name).subscribe({
        next: (res) => {
          this.uploading.set(false);
          this.setValue(res.url);
        },
        error: () => {
          this.uploading.set(false);
          this.toast.error(this.lang.t('image.failed'));
        },
      });
    } catch {
      this.uploading.set(false);
      this.toast.error(this.lang.t('image.unreadable'));
    }
  }
}

/** Shrinks big photos to MAX_SIDE and re-encodes them as JPEG; HEIC works where the browser can decode it. */
async function prepareImage(file: File): Promise<{ blob: Blob; name: string }> {
  const img = await loadImage(file);
  const scale = Math.min(1, MAX_SIDE / Math.max(img.naturalWidth, img.naturalHeight));

  if (scale === 1 && file.size <= KEEP_ORIGINAL_BYTES && WEB_TYPES.includes(file.type)) {
    return { blob: file, name: file.name };
  }

  const canvas = document.createElement('canvas');
  canvas.width = Math.round(img.naturalWidth * scale);
  canvas.height = Math.round(img.naturalHeight * scale);
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas unavailable');
  // JPEG has no transparency: paint white under transparent PNGs instead of black.
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.85));
  if (!blob) throw new Error('Encoding failed');
  const base = file.name.replace(/\.[^.]+$/, '') || 'image';
  return { blob, name: `${base}.jpg` };
}

function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('Unreadable image'));
    };
    img.src = url;
  });
}
