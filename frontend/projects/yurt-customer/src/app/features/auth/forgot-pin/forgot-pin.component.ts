import { Component, inject, signal, viewChild, viewChildren, ElementRef, OnDestroy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { YurtApiService, AuthStateService } from 'shared-api';
import { ButtonComponent, OtpBoxesComponent, ToastService } from 'shared-ui';
import { TranslatePipe } from '../../../core/translate.pipe';
import { LangService } from '../../../core/lang.service';
import { OtpProof, PhoneOtpService, firebaseOtpError } from '../../../core/phone-otp.service';

/** "Forgot PIN": phone → WhatsApp code or Firebase SMS code + new PIN → signed in. */
@Component({
  selector: 'app-forgot-pin',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, OtpBoxesComponent, TranslatePipe],
  templateUrl: './forgot-pin.component.html',
  styleUrl: '../login/login.component.css',
})
export class ForgotPinComponent implements OnDestroy {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private lang = inject(LangService);
  /** WhatsApp (4 digits) or Firebase SMS (6 digits), whichever the backend picked. */
  readonly otpSession = inject(PhoneOtpService).session();
  /** Set when Firebase verified the number on its own (Android), so no code is needed. */
  readonly verifiedProof = signal<OtpProof | null>(null);

  readonly pinInputs = viewChildren<ElementRef>('pinInput');
  readonly otpBoxes = viewChild<OtpBoxesComponent>('otpBoxes');

  step = signal<'phone' | 'code'>('phone');
  phoneFormatted = '';
  pins: string[] = ['', '', '', ''];
  otp = '';
  loading = signal(false);
  error = signal('');
  resendIn = signal(0);

  private pendingPhone = '';
  private resendTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    this.clearResendTimer();
  }

  get phoneDigits(): string {
    return this.phoneFormatted.replace(/\D/g, '').slice(-10);
  }

  get pin4(): string {
    return this.pins.join('');
  }

  private formatPhone(raw: string): string {
    let digits = raw.replace(/\D/g, '');
    if (raw.trimStart().startsWith('+')) {
      digits = digits.slice(1);
    } else if (digits.length > 10) {
      digits = digits.slice(1);
    }
    const s = digits.slice(0, 10);
    if (!s.length) return '';
    if (s.length <= 3) return `+7 (${s}`;
    if (s.length <= 6) return `+7 (${s.slice(0, 3)}) ${s.slice(3)}`;
    return `+7 (${s.slice(0, 3)}) ${s.slice(3, 6)}-${s.slice(6)}`;
  }

  onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const formatted = this.formatPhone(input.value);
    this.phoneFormatted = formatted;
    input.value = formatted;
  }

  onPinInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.pins[index] = val;
    if (val && index < 3) this.pinInputs()[index + 1]?.nativeElement.focus();
  }

  onPinKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.pins[index] && index > 0)
      this.pinInputs()[index - 1]?.nativeElement.focus();
    if (event.key === 'Enter') this.onSubmit();
  }

  onSendCode(): void {
    this.error.set('');
    if (!/^\d{10}$/.test(this.phoneDigits)) {
      this.error.set('Enter your 10-digit mobile number.');
      return;
    }
    this.requestCode('+7' + this.phoneDigits, () => {
      this.otp = '';
      this.pins = ['', '', '', ''];
      this.step.set('code');
    });
  }

  onResend(): void {
    if (this.resendIn() > 0) return;
    this.requestCode(this.pendingPhone, () => this.otpBoxes()?.clear(), true);
  }

  private requestCode(phone: string, onSent: () => void, resend = false): void {
    this.loading.set(true);
    this.api.pinResetStart(phone).subscribe({
      next: (res) => {
        this.pendingPhone = res.mobileNumber || phone;
        this.verifiedProof.set(null);
        this.otpSession.begin(res, resend).then(
          (instant) => {
            this.loading.set(false);
            this.verifiedProof.set(instant);
            onSent();
            this.startResendCountdown();
            if (res.devCode) this.toast.success('Dev code: ' + res.devCode);
          },
          (err) => {
            this.loading.set(false);
            this.error.set(firebaseOtpError(err, 'Could not send the SMS. Try again.'));
          },
        );
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.status === 422 ? this.lang.t('auth.noWhatsApp') : (err.error?.title ?? 'Could not send the code.'));
      },
    });
  }

  onSubmit(): void {
    this.error.set('');
    const verified = this.verifiedProof();
    if (!verified && !this.otpSession.isComplete(this.otp)) {
      this.error.set(`Enter the ${this.otpSession.codeLength()}-digit code.`);
      return;
    }
    if (this.pin4.length !== 4) {
      this.error.set('Enter a 4-digit PIN.');
      return;
    }

    this.loading.set(true);
    (verified ? Promise.resolve(verified) : this.otpSession.proof(this.otp)).then(
      (proof) => this.submitProof(proof),
      (err) => {
        this.loading.set(false);
        this.error.set(firebaseOtpError(err, 'Incorrect code.'));
      },
    );
  }

  private submitProof(proof: OtpProof): void {
    this.api.pinResetVerify(this.pendingPhone, proof, this.pin4).subscribe({
      next: (res) => {
        this.auth.setUser({
          accessToken: res.accessToken,
          refreshToken: res.refreshToken,
          userId: res.userId,
          displayName: res.displayName,
          userType: res.userType,
        });
        this.toast.success(this.lang.t('auth.resetDone'));
        this.router.navigate(['/locations']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.title ?? 'Could not reset the PIN.');
      },
    });
  }

  onBack(): void {
    this.error.set('');
    this.clearResendTimer();
    this.resendIn.set(0);
    this.step.set('phone');
  }

  private startResendCountdown(): void {
    this.clearResendTimer();
    this.resendIn.set(60);
    this.resendTimer = setInterval(() => {
      const next = this.resendIn() - 1;
      this.resendIn.set(next);
      if (next <= 0) this.clearResendTimer();
    }, 1000);
  }

  private clearResendTimer(): void {
    if (this.resendTimer) {
      clearInterval(this.resendTimer);
      this.resendTimer = null;
    }
  }
}
