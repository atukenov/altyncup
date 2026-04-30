import { Component, inject, signal, viewChildren, ElementRef } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { YurtApiService, AuthStateService } from 'shared-api';
import { ButtonComponent, ToastService } from 'shared-ui';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
})
export class RegisterComponent {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);

  readonly pinInputs = viewChildren<ElementRef>('pinInput');
  readonly confirmInputs = viewChildren<ElementRef>('confirmInput');

  mobileNumber = '';
  firstName = '';
  lastName = '';
  pins: string[] = ['', '', '', ''];
  confirmPins: string[] = ['', '', '', ''];
  loading = signal(false);
  error = signal('');

  get pin4(): string {
    return this.pins.join('');
  }
  get confirmPin4(): string {
    return this.confirmPins.join('');
  }

  onPinInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.pins[index] = val;
    if (val && index < 3) this.pinInputs()[index + 1]?.nativeElement.focus();
  }

  onPinKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.pins[index] && index > 0)
      this.pinInputs()[index - 1]?.nativeElement.focus();
  }

  onConfirmInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.confirmPins[index] = val;
    if (val && index < 3) this.confirmInputs()[index + 1]?.nativeElement.focus();
  }

  onRegister(): void {
    this.error.set('');
    if (!this.firstName.trim()) {
      this.error.set('Enter your first name.');
      return;
    }
    if (!this.lastName.trim()) {
      this.error.set('Enter your last name.');
      return;
    }
    if (!this.mobileNumber.trim()) {
      this.error.set('Enter your mobile number.');
      return;
    }
    if (this.pin4.length !== 4) {
      this.error.set('Enter a 4-digit PIN.');
      return;
    }
    if (this.pin4 !== this.confirmPin4) {
      this.error.set('PINs do not match.');
      return;
    }

    this.loading.set(true);
    this.api
      .register(this.mobileNumber, this.pin4, this.firstName.trim(), this.lastName.trim())
      .subscribe({
        next: (res) => {
          this.auth.setUser({
            token: res.token,
            userId: res.userId,
            displayName: res.displayName,
            userType: res.userType,
          });
          this.toast.success('Account created! Welcome to Yurt ☕');
          this.router.navigate(['/locations']);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(err.error?.title ?? 'Registration failed.');
        },
      });
  }
}
