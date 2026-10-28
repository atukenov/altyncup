import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { OtpDeliveryStatus, OtpEventEntry, OtpEventType, OtpPendingCode, OtpSummary, WhatsAppInstanceInfo } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

const PAGE_SIZE = 50;
/** How often the re-link panel checks whether the phone has linked (and refreshes the QR). */
const RELINK_POLL_MS = 5000;
/** Stop polling after this long so an abandoned tab does not hit Green API forever. */
const RELINK_TIMEOUT_MS = 3 * 60 * 1000;
const KNOWN_STATES = ['authorized', 'notAuthorized', 'blocked', 'yellowCard', 'starting', 'sleepMode'];

/** WhatsApp OTP health: instance status (with QR / phone-code re-link), outcome counts, pending codes (with resend), people who never got through, raw history. */
@Component({
  selector: 'app-otp-log',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './otp-log.component.html',
})
export class OtpLogComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private confirmSvc = inject(ConfirmService);
  private lang = inject(AdminLangService);

  instance = signal<WhatsAppInstanceInfo | null>(null);
  instanceLoading = signal(true);

  /** Re-link panel: null when closed. */
  relinkMode = signal<'qr' | 'phone' | null>(null);
  qrImage = signal<string | null>(null);
  qrError = signal<string | null>(null);
  relinkTimedOut = signal(false);
  relinkPhone = '';
  authCode = signal<string | null>(null);
  authCodeError = signal<string | null>(null);
  authCodeLoading = signal(false);
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private pollStartedAt = 0;

  summary = signal<OtpSummary | null>(null);
  summaryLoading = signal(true);
  hours = 24;

  events = signal<OtpEventEntry[]>([]);
  eventsLoading = signal(true);
  total = signal(0);
  page = signal(1);
  phoneFilter = '';
  typeFilter: OtpEventType | '' = '';

  codes = signal<OtpPendingCode[]>([]);
  codesLoading = signal(true);
  resendingId = signal<string | null>(null);

  alertMessage = signal<{ ok: boolean; key: string; suffix?: string } | null>(null);
  sendingAlert = signal(false);

  readonly tiles: { type: OtpEventType; key: string; tone: string }[] = [
    { type: 'CodeSent', key: 'otp.sent', tone: 'text-slate-800' },
    { type: 'SendFailed', key: 'otp.failed', tone: 'text-red-600' },
    { type: 'Verified', key: 'otp.verified', tone: 'text-emerald-600' },
    { type: 'WrongCode', key: 'otp.wrongCode', tone: 'text-amber-600' },
    { type: 'Expired', key: 'otp.expired', tone: 'text-slate-500' },
  ];
  readonly types: OtpEventType[] = [
    'CodeSent', 'SendFailed', 'DeliveryFailed', 'DeliveryDisabled', 'Verified', 'WrongCode', 'Expired', 'TooManyAttempts', 'NoWhatsApp',
  ];

  get hasMore(): boolean {
    return this.events().length < this.total();
  }

  get whatsappOk(): boolean {
    return this.instance()?.state === 'authorized';
  }

  get canRelink(): boolean {
    return this.instance()?.state === 'notAuthorized';
  }

  get stateKey(): string {
    const state = this.instance()?.state;
    return 'wa.state.' + (state && KNOWN_STATES.includes(state) ? state : 'unknown');
  }

  ngOnInit(): void {
    this.loadInstance();
    this.loadSummary();
    this.loadCodes();
    this.loadEvents();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadInstance(): void {
    this.instanceLoading.set(true);
    this.api.getWhatsAppInstance().subscribe({
      next: (info) => { this.applyInstance(info); this.instanceLoading.set(false); },
      error: () => this.instanceLoading.set(false),
    });
  }

  /** "+7 701 123 45 67" from Green API's digits-only phone. */
  formatPhone(digits: string): string {
    const m = /^7(\d{3})(\d{3})(\d{2})(\d{2})$/.exec(digits);
    return m ? `+7 ${m[1]} ${m[2]} ${m[3]} ${m[4]}` : `+${digits}`;
  }

  startRelink(mode: 'qr' | 'phone' = 'qr'): void {
    this.relinkMode.set(mode);
    this.relinkTimedOut.set(false);
    this.qrImage.set(null);
    this.qrError.set(null);
    this.authCode.set(null);
    this.authCodeError.set(null);
    if (mode === 'qr') this.fetchQr();
    this.startPolling();
  }

  cancelRelink(): void {
    this.stopPolling();
    this.relinkMode.set(null);
    this.loadInstance();
  }

  requestAuthCode(): void {
    const phone = this.relinkPhone.trim();
    if (!phone) return;
    this.authCodeLoading.set(true);
    this.authCode.set(null);
    this.authCodeError.set(null);
    this.api.getWhatsAppAuthorizationCode(phone).subscribe({
      next: (res) => {
        this.authCode.set(res.code);
        this.authCodeLoading.set(false);
        this.startPolling();
      },
      error: (err) => {
        this.authCodeError.set(err?.error?.title ?? err?.message ?? '');
        this.authCodeLoading.set(false);
      },
    });
  }

  private fetchQr(): void {
    this.api.getWhatsAppQr().subscribe({
      next: (qr) => {
        if (qr.type === 'alreadyLogged') { this.onLinked(); return; }
        if (qr.type === 'qrCode' && qr.image) {
          this.qrImage.set(qr.image);
          this.qrError.set(null);
        } else {
          this.qrError.set(qr.message ?? qr.type);
        }
      },
      error: (err) => this.qrError.set(err?.message ?? 'error'),
    });
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollStartedAt = Date.now();
    this.pollTimer = setInterval(() => this.poll(), RELINK_POLL_MS);
  }

  private stopPolling(): void {
    if (this.pollTimer) clearInterval(this.pollTimer);
    this.pollTimer = null;
  }

  private poll(): void {
    if (Date.now() - this.pollStartedAt > RELINK_TIMEOUT_MS) {
      this.stopPolling();
      this.relinkTimedOut.set(true);
      this.qrImage.set(null);
      return;
    }
    // Green API rotates the QR about every 20s; a fresh fetch also reports "alreadyLogged" once scanned.
    // In phone mode a new QR could cancel the pairing code, so only the state is checked.
    if (this.relinkMode() === 'qr') {
      this.fetchQr();
    } else {
      this.api.getWhatsAppInstance().subscribe({
        next: (info) => { if (info.state === 'authorized') this.onLinked(info); },
      });
    }
  }

  private onLinked(info?: WhatsAppInstanceInfo): void {
    this.stopPolling();
    this.relinkMode.set(null);
    this.alertMessage.set({ ok: true, key: 'wa.linked' });
    if (info) this.applyInstance(info);
    else this.loadInstance();
    this.loadSummary();
  }

  private applyInstance(info: WhatsAppInstanceInfo): void {
    this.instance.set(info);
    // The panel only makes sense while logged out.
    if (info.state !== 'notAuthorized' && this.relinkMode()) {
      this.stopPolling();
      this.relinkMode.set(null);
    }
  }

  loadCodes(): void {
    this.codesLoading.set(true);
    this.api.getOtpCodes().subscribe({
      next: (res) => { this.codes.set(res.items); this.codesLoading.set(false); },
      error: () => this.codesLoading.set(false),
    });
  }

  async resend(row: OtpPendingCode): Promise<void> {
    const ok = await this.confirmSvc.confirm(
      this.lang.t('otp.resendTitle'),
      `${this.lang.t('otp.resendMessage')} ${row.mobileNumber}`,
      this.lang.t('otp.resend'),
      '📲',
    );
    if (!ok) return;

    this.resendingId.set(row.id);
    this.api.resendOtpCode(row.id).subscribe({
      next: () => {
        this.alertMessage.set({ ok: true, key: 'otp.resendOk', suffix: row.mobileNumber });
        this.resendingId.set(null);
        this.loadCodes();
        this.loadSummary();
        this.loadEvents();
      },
      error: () => {
        this.alertMessage.set({ ok: false, key: 'otp.resendFailed', suffix: row.mobileNumber });
        this.resendingId.set(null);
        // A failed send still saves the new code, so refresh to show it.
        this.loadCodes();
        this.loadEvents();
      },
    });
  }

  loadSummary(): void {
    this.summaryLoading.set(true);
    this.api.getOtpSummary(this.hours).subscribe({
      next: (s) => { this.summary.set(s); this.summaryLoading.set(false); },
      error: () => this.summaryLoading.set(false),
    });
  }

  loadEvents(append = false): void {
    this.eventsLoading.set(true);
    this.api.getOtpEvents({
      mobileNumber: this.phoneFilter.trim() || undefined,
      type: this.typeFilter || undefined,
      page: this.page(),
      pageSize: PAGE_SIZE,
    }).subscribe({
      next: (res) => {
        this.total.set(res.total);
        this.events.update(prev => append ? [...prev, ...res.items] : res.items);
        this.eventsLoading.set(false);
      },
      error: () => this.eventsLoading.set(false),
    });
  }

  applyFilter(): void {
    this.page.set(1);
    this.loadEvents();
  }

  loadMore(): void {
    this.page.update(p => p + 1);
    this.loadEvents(true);
  }

  showPhone(mobile: string): void {
    this.phoneFilter = mobile;
    this.typeFilter = '';
    this.applyFilter();
  }

  sendTestAlert(): void {
    this.sendingAlert.set(true);
    this.api.sendOtpTestAlert().subscribe({
      next: () => { this.alertMessage.set({ ok: true, key: 'otp.alertSent' }); this.sendingAlert.set(false); },
      error: () => { this.alertMessage.set({ ok: false, key: 'otp.alertFailed' }); this.sendingAlert.set(false); },
    });
  }

  typeClass(type: OtpEventType): string {
    switch (type) {
      case 'Verified': return 'bg-emerald-50 text-emerald-700';
      case 'SendFailed': case 'DeliveryFailed': case 'TooManyAttempts': return 'bg-red-50 text-red-700';
      case 'WrongCode': case 'Expired': case 'NoWhatsApp': return 'bg-amber-50 text-amber-700';
      default: return 'bg-slate-100 text-slate-700';
    }
  }

  /** Translation key for a code's delivery status; no status yet means queued. */
  deliveryKey(status?: OtpDeliveryStatus | null): string {
    return 'otp.delivery.' + (status ?? 'queued');
  }

  deliveryClass(status?: OtpDeliveryStatus | null): string {
    switch (status) {
      case 'delivered': case 'read': return 'bg-emerald-50 text-emerald-700';
      case 'sent': return 'bg-sky-50 text-sky-700';
      case 'failed': case 'noAccount': case 'notInGroup': case 'sendFailed': return 'bg-red-50 text-red-700';
      default: return 'bg-slate-100 text-slate-600';
    }
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleString();
  }
}
