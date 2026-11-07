import { Component, Input } from '@angular/core';
import { TranslatePipe } from './translate.pipe';

/** Where a phone-verification attempt is, as far as the person needs to know. */
export type OtpStatus = 'sending' | 'resending' | 'sent' | 'checking' | null;

/**
 * One status line for the phone-verification screens, so a tap on "send code" / "verify"
 * never looks like nothing happened: a spinner while the code is on its way or being
 * checked, then a green "code sent to …" line once it has gone out.
 */
@Component({
  selector: 'app-otp-status',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    @switch (status) {
      @case ('sent') {
        <div class="otp-status otp-status--sent" role="status" aria-live="polite">
          <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
          </svg>
          <span>
            {{ (firebase ? 'otp.sentSms' : 'otp.sentWhatsApp') | t }}@if (phone) {<span class="otp-status__phone">&nbsp;{{ phone }}</span>}
          </span>
        </div>
      }
      @case (null) {}
      @default {
        <div class="otp-status otp-status--busy" role="status" aria-live="polite">
          <span class="otp-status__spinner" aria-hidden="true"></span>
          <span>
            <span class="otp-status__title">{{ ('otp.' + status) | t }}</span>
            @if (status !== 'checking') {
              <span class="otp-status__hint">{{ 'otp.sendingHint' | t }}</span>
            }
          </span>
        </div>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .otp-status {
      display: flex; align-items: flex-start; gap: 8px;
      padding: 10px 12px; border-radius: 12px;
      font-size: 13px; font-weight: 600; line-height: 1.35;
    }
    .otp-status svg { flex-shrink: 0; margin-top: 1px; }
    .otp-status--sent { background: #ecfdf5; border: 1px solid #a7f3d0; color: #047857; }
    .otp-status--busy { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
    .otp-status__phone { white-space: nowrap; font-weight: 700; }
    .otp-status__title { display: block; }
    .otp-status__hint { display: block; margin-top: 2px; font-size: 11.5px; font-weight: 500; color: #b45309; }
    .otp-status__spinner {
      flex-shrink: 0; width: 16px; height: 16px; margin-top: 1px;
      border: 2px solid currentColor; border-top-color: transparent; border-radius: 50%;
      animation: otp-spin .8s linear infinite;
    }
    @keyframes otp-spin { to { transform: rotate(360deg); } }
  `,
})
export class OtpStatusComponent {
  @Input() status: OtpStatus = null;
  /** True when the code goes by SMS (Firebase), false for WhatsApp. */
  @Input() firebase = false;
  @Input() phone = '';
}
