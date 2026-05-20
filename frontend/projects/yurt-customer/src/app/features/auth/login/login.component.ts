import { Component, inject, signal, ElementRef, viewChildren } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { YurtApiService, AuthStateService } from 'shared-api';
import { ButtonComponent, ToastService } from 'shared-ui';
import { TranslatePipe } from '../../../core/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);

  readonly pinInputs = viewChildren<ElementRef>('pinInput');

  phoneDigits = '';
  pins: string[] = ['', '', '', ''];
  loading = signal(false);
  error = signal('');

  get pin4(): string {
    return this.pins.join('');
  }

  onPinInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.pins[index] = val.replace(/\D/g, '').slice(-1);
    if (this.pins[index] && index < 3) {
      this.pinInputs()[index + 1]?.nativeElement.focus();
    }
    if (index === 3 && this.pins[3]) {
      this.onLogin();
    }
  }

  onPinKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.pins[index] && index > 0) {
      this.pinInputs()[index - 1]?.nativeElement.focus();
    }
    if (event.key === 'Enter') this.onLogin();
  }

  onPaste(event: ClipboardEvent): void {
    const pasted = event.clipboardData?.getData('text')?.replace(/\D/g, '').slice(0, 4) ?? '';
    pasted.split('').forEach((char, i) => {
      this.pins[i] = char;
    });
    this.pinInputs()[Math.min(pasted.length, 3)]?.nativeElement.focus();
    event.preventDefault();
    if (pasted.length === 4) {
      this.onLogin();
    }
  }

  onLogin(): void {
    this.error.set('');
    if (!/^\d{10}$/.test(this.phoneDigits)) {
      this.error.set('Enter your 10-digit mobile number.');
      return;
    }
    if (this.pin4.length !== 4) {
      this.error.set('Enter your 4-digit PIN.');
      return;
    }

    this.loading.set(true);
    this.api.login('+7' + this.phoneDigits, this.pin4).subscribe({
      next: (res) => {
        this.auth.setUser({
          token: res.token,
          userId: res.userId,
          displayName: res.displayName,
          userType: res.userType,
        });
        this.router.navigate(['/locations']);
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err.error?.title ?? err.error?.detail ?? 'Login failed. Please try again.';
        this.error.set(msg);
        this.pins = ['', '', '', ''];
        this.pinInputs()[0]?.nativeElement.focus();
      },
    });
  }
}