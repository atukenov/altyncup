import { Component, ElementRef, EventEmitter, Input, Output, viewChildren } from '@angular/core';
import { CommonModule } from '@angular/common';

/** A row of single-digit boxes for entering a numeric verification code — focus advances
 * on input and returns to the previous box on backspace. Used for both the mobile-number
 * OTP step and the 4-digit PIN entry that share this exact interaction pattern. The number
 * of boxes follows {@link length}, e.g. 4 for WhatsApp codes and 6 for Firebase SMS codes. */
@Component({
  selector: 'yurt-otp-boxes',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="flex justify-center" [class.gap-3]="!compact" [class.gap-2]="compact">
      @for (i of indices; track i) {
        <input
          #box
          type="text"
          inputmode="numeric"
          [attr.maxlength]="length"
          [value]="digits[i]"
          (input)="onInput(i, $event)"
          (paste)="onPaste(i, $event)"
          (keydown)="onKeydown(i, $event)"
          autocomplete="one-time-code"
          class="pin-box"
          [class.compact]="compact"
          [attr.aria-label]="'Code digit ' + (i + 1)"
        />
      }
    </div>
  `,
  styles: [
    `
      .pin-box {
        width: 3.5rem;
        height: 3.5rem;
        text-align: center;
        font-size: 1.5rem;
        font-weight: 700;
        border: 2px solid #e7e5e4;
        border-radius: 1rem;
        background: #fff;
        outline: none;
        transition: all 0.15s;
      }
      .pin-box.compact {
        width: 2.75rem;
        height: 3.25rem;
        font-size: 1.375rem;
        border-radius: 0.875rem;
      }
      .pin-box:focus {
        border-color: #fbbf24;
        box-shadow: 0 0 0 2px rgba(253, 230, 138, 0.6);
      }
    `,
  ],
})
export class OtpBoxesComponent {
  @Output() codeChange = new EventEmitter<string>();

  private readonly boxes = viewChildren<ElementRef<HTMLInputElement>>('box');

  digits: string[] = Array(4).fill('');
  indices: number[] = [0, 1, 2, 3];
  private _length = 4;

  /** Number of boxes. Changing it clears whatever was typed. */
  @Input()
  set length(value: number) {
    if (value === this._length) return;
    this._length = value;
    this.indices = Array.from({ length: value }, (_, i) => i);
    this.digits = Array(value).fill('');
    this.codeChange.emit('');
  }
  get length(): number {
    return this._length;
  }

  /** Six boxes don't fit a phone screen at the 4-box size. */
  get compact(): boolean {
    return this._length > 4;
  }

  /**
   * One typed digit moves to the next box. Several digits at once (the keyboard's SMS / WhatsApp
   * code suggestion, iOS "From Messages", or an autofilled one-time-code) are spread over the
   * boxes from this one on, instead of keeping only the last digit.
   */
  onInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const typed = input.value.replace(/\D/g, '');
    if (typed.length > 1) {
      // A box that already held a digit gets the new one appended; drop the old one unless
      // the whole code arrived at once.
      const incoming = typed.length > this.length - index ? typed : typed.slice(this.digits[index] ? 1 : 0);
      this.fill(index, incoming, input);
      return;
    }
    this.digits[index] = typed;
    input.value = typed;
    if (typed && index < this.length - 1) this.boxes()[index + 1]?.nativeElement.focus();
    this.codeChange.emit(this.digits.join(''));
  }

  onPaste(index: number, event: ClipboardEvent): void {
    const pasted = (event.clipboardData?.getData('text') ?? '').replace(/\D/g, '');
    if (!pasted) return;
    event.preventDefault();
    this.fill(index, pasted, event.target as HTMLInputElement);
  }

  /** Put digits into the boxes: a full-length code always starts at the first box. */
  private fill(index: number, digits: string, source: HTMLInputElement): void {
    const start = digits.length >= this.length ? 0 : index;
    const chunk = digits.slice(0, this.length - start);
    const next = [...this.digits];
    for (let k = 0; k < chunk.length; k++) next[start + k] = chunk[k];
    this.digits = next;
    // The box the text landed in holds the raw text until change detection, so set it now.
    source.value = this.digits[index] ?? '';
    const boxes = this.boxes();
    boxes.forEach((b, k) => (b.nativeElement.value = this.digits[k] ?? ''));
    const last = Math.min(start + chunk.length, this.length - 1);
    boxes[last]?.nativeElement.focus();
    this.codeChange.emit(this.digits.join(''));
  }

  onKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.digits[index] && index > 0)
      this.boxes()[index - 1]?.nativeElement.focus();
  }

  /** Reset all boxes to empty and refocus the first one — used before resending a code. */
  clear(): void {
    this.digits = Array(this.length).fill('');
    this.codeChange.emit('');
    this.boxes()[0]?.nativeElement.focus();
  }
}
