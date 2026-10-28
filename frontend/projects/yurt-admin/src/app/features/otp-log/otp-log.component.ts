import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { OtpDeliveryStatus, OtpEventEntry, OtpEventType, OtpPendingCode, OtpSummary } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

const PAGE_SIZE = 50;

/** WhatsApp OTP health: instance status, outcome counts, pending codes (with resend), people who never got through, raw history. */
@Component({
  selector: 'app-otp-log',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './otp-log.component.html',
})
export class OtpLogComponent implements OnInit {
  private api = inject(YurtApiService);
  private confirmSvc = inject(ConfirmService);
  private lang = inject(AdminLangService);

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
    return this.summary()?.whatsappState === 'authorized';
  }

  ngOnInit(): void {
    this.loadSummary();
    this.loadCodes();
    this.loadEvents();
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
