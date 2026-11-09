import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import {
  IikoActivityEntry,
  IikoOrderRow,
  IikoOverview,
  IikoPaymentType,
  IikoTerminalGroup,
} from 'shared-models';
import { BadgeVariant, Currency2Pipe, ToastService } from 'shared-ui';

const ORDER_PAGE_SIZE = 20;
const ACTIVITY_PAGE_SIZE = 30;

interface SyncMeta {
  label: string;
  variant: BadgeVariant;
  hint: string;
}

const SYNC: Record<string, SyncMeta> = {
  NotPushed: { label: 'Not sent', variant: 'slate', hint: 'Waiting for preparation to start, or the order started before iiko push was on.' },
  Pushed: { label: 'In iiko', variant: 'teal', hint: 'Created in iiko. The cashier sees it on the delivery screen.' },
  Closed: { label: 'Closed in iiko', variant: 'green', hint: 'Completed and closed in iiko.' },
  SkippedUnmapped: { label: 'Not mapped', variant: 'amber', hint: 'An item or the location has no iiko mapping. Fix it, then press Send to iiko.' },
  Failed: { label: 'Failed', variant: 'red', hint: 'iiko did not accept the order. It is not retried automatically: press Send to iiko.' },
};

const EVENT_LABEL: Record<string, string> = {
  IikoOrderPushed: 'Sent to iiko',
  IikoOrderClosed: 'Closed in iiko',
  IikoOrderSkippedUnmapped: 'Skipped: not mapped',
  IikoOrderPushFailed: 'Send failed',
  IikoOrderCloseFailed: 'Close failed',
  IikoOrderWebhookProblem: 'iiko reported a problem',
};

/** Super-admin view of everything iiko: setup status, order push results, mapping coverage and the iiko audit trail. */
@Component({
  selector: 'app-iiko-overview',
  standalone: true,
  imports: [CommonModule, RouterLink, Currency2Pipe],
  templateUrl: './iiko-overview.component.html',
})
export class IikoOverviewComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  readonly pageSize = ORDER_PAGE_SIZE;
  readonly statusFilters = ['NotPushed', 'Pushed', 'Closed', 'SkippedUnmapped', 'Failed'];

  overview = signal<IikoOverview | null>(null);
  overviewError = signal('');

  orders = signal<IikoOrderRow[]>([]);
  ordersTotal = signal(0);
  ordersPage = signal(1);
  ordersLoading = signal(true);
  syncFilter = signal('');
  retrying = signal<string | null>(null);

  activity = signal<IikoActivityEntry[]>([]);
  activityTotal = signal(0);
  activityPage = signal(1);
  activityLoading = signal(true);

  // Live check against iiko (terminal groups + payment types), run on demand.
  checking = signal(false);
  checkError = signal('');
  terminalGroups = signal<IikoTerminalGroup[] | null>(null);
  paymentTypes = signal<IikoPaymentType[] | null>(null);

  readonly ordersTotalPages = computed(() => Math.max(1, Math.ceil(this.ordersTotal() / ORDER_PAGE_SIZE)));
  readonly activityHasMore = computed(() => this.activity().length < this.activityTotal());

  /** Setup checklist derived from the server's flags (never the secrets themselves). */
  readonly setup = computed(() => {
    const c = this.overview()?.config;
    if (!c) return [];
    return [
      { label: 'iiko integration', ok: c.enabled, hint: 'Iiko:Enabled must be true on the server.' },
      { label: 'Send orders to iiko', ok: c.pushOrdersEnabled, hint: 'Iiko:PushOrdersEnabled must be true.' },
      { label: 'Credentials', ok: c.credentialsConfigured, hint: 'Iiko:ApiKey (or AppId and ClientSecret) is missing.' },
      { label: 'Organization', ok: c.organizationConfigured, hint: 'Iiko:OrganizationId is missing.' },
      { label: 'Payment type', ok: c.paymentTypeConfigured, hint: 'Iiko:PaymentTypeId is missing. Orders are sent as already paid with this type.' },
      { label: 'Loyalty program', ok: c.programConfigured, hint: 'Iiko:ProgramId is missing; bonus points will not work.' },
      { label: 'Webhook secret', ok: c.webhookTokenConfigured, hint: 'Iiko:WebhookAuthToken is missing; iiko cannot report order problems back.' },
    ];
  });

  readonly setupProblems = computed(() => this.setup().filter((s) => !s.ok).length);

  readonly unmappedLocations = computed(() =>
    (this.overview()?.locations ?? []).filter((l) => l.isActive && !l.terminalGroupId));

  ngOnInit(): void {
    this.loadOverview();
    this.loadOrders();
    this.loadActivity();
  }

  refresh(): void {
    this.loadOverview();
    this.loadOrders();
    this.activityPage.set(1);
    this.loadActivity();
  }

  loadOverview(): void {
    this.overviewError.set('');
    this.api.getIikoOverview().subscribe({
      next: (o) => this.overview.set(o),
      error: () => this.overviewError.set('Could not load the iiko overview.'),
    });
  }

  loadOrders(): void {
    this.ordersLoading.set(true);
    this.api
      .getIikoOrders({ syncStatus: this.syncFilter() || undefined, page: this.ordersPage(), pageSize: ORDER_PAGE_SIZE })
      .subscribe({
        next: (res) => {
          this.orders.set(res.items);
          this.ordersTotal.set(res.total);
          this.ordersLoading.set(false);
        },
        error: () => this.ordersLoading.set(false),
      });
  }

  loadActivity(append = false): void {
    this.activityLoading.set(true);
    this.api.getIikoActivity(this.activityPage(), ACTIVITY_PAGE_SIZE).subscribe({
      next: (res) => {
        this.activityTotal.set(res.total);
        this.activity.update((prev) => (append ? [...prev, ...res.items] : res.items));
        this.activityLoading.set(false);
      },
      error: () => this.activityLoading.set(false),
    });
  }

  filterBy(status: string): void {
    this.syncFilter.set(this.syncFilter() === status ? '' : status);
    this.ordersPage.set(1);
    this.loadOrders();
  }

  goToOrdersPage(page: number): void {
    this.ordersPage.set(Math.min(Math.max(1, page), this.ordersTotalPages()));
    this.loadOrders();
  }

  loadMoreActivity(): void {
    this.activityPage.update((p) => p + 1);
    this.loadActivity(true);
  }

  count(status: string): number {
    return this.overview()?.orders.byStatus[status] ?? 0;
  }

  sync(status: string): SyncMeta {
    return SYNC[status] ?? { label: status, variant: 'slate', hint: '' };
  }

  eventLabel(action?: string | null): string {
    return action ? (EVENT_LABEL[action] ?? action) : '';
  }

  canRetry(o: IikoOrderRow): boolean {
    // Nothing is re-sent automatically; this button is the only way to try again.
    if (o.status === 'Completed') return o.syncStatus === 'Pushed' && o.pendingAction === 'Close';
    if (o.syncStatus === 'SkippedUnmapped' || o.syncStatus === 'Failed') {
      return o.status === 'Accepted' || o.status === 'Preparing' || o.status === 'Ready';
    }
    return o.syncStatus === 'NotPushed' && (o.status === 'Preparing' || o.status === 'Ready');
  }

  retry(o: IikoOrderRow): void {
    this.retrying.set(o.id);
    this.api.retryIikoPush(o.id).subscribe({
      next: () => {
        this.retrying.set(null);
        this.toast.success('Sent to iiko');
        this.refresh();
      },
      error: (err) => {
        this.retrying.set(null);
        this.toast.error(err?.error?.detail ?? err?.error?.title ?? 'Could not send to iiko.');
        this.refresh();
      },
    });
  }

  /** Calls iiko for real: proves credentials work and lets us show terminal group and payment type names. */
  checkConnection(): void {
    this.checking.set(true);
    this.checkError.set('');
    this.api.getIikoTerminalGroups().subscribe({
      next: (groups) => {
        this.terminalGroups.set(groups);
        this.api.getIikoPaymentTypes().subscribe({
          next: (types) => {
            this.paymentTypes.set(types);
            this.checking.set(false);
            this.toast.success(`Connected to iiko: ${groups.length} terminal groups, ${types.length} payment types`);
          },
          error: (err) => this.failCheck(err),
        });
      },
      error: (err) => this.failCheck(err),
    });
  }

  private failCheck(err: { error?: { title?: string; detail?: string } }): void {
    this.checking.set(false);
    const e = err?.error;
    this.checkError.set([e?.title, e?.detail].filter(Boolean).join(' ') || 'Could not reach iiko.');
  }

  terminalGroupName(id?: string | null): string {
    if (!id) return '';
    return this.terminalGroups()?.find((g) => g.id === id)?.name ?? id;
  }

  shortId(id: string): string {
    return id.slice(-6).toUpperCase();
  }

  formatDate(iso?: string | null): string {
    return iso ? new Date(iso).toLocaleString() : '';
  }
}
