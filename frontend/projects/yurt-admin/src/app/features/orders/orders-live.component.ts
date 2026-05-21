import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService, SignalrService } from 'shared-api';
import { Order, OrderStatus, Location } from 'shared-models';
import {
  BadgeComponent,
  ButtonComponent,
  ToastService,
  OrderStatusColorPipe,
  Currency2Pipe,
} from 'shared-ui';
import { environment } from '../../../environments/environment';
import { Subscription } from 'rxjs';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-orders-live',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    BadgeComponent,
    ButtonComponent,
    OrderStatusColorPipe,
    Currency2Pipe,
    AdminTranslatePipe,
  ],
  templateUrl: './orders-live.component.html',
  styleUrl: './orders-live.component.css',
})
export class OrdersLiveComponent implements OnInit, OnDestroy {
  readonly signalr = inject(SignalrService);
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  readonly OrderStatus = OrderStatus;

  orders = signal<Order[]>([]);
  locations = signal<Location[]>([]);
  loading = signal(true);
  selectedOrder = signal<Order | null>(null);
  activeTab = signal<string>('active');
  selectedLocationId = '';

  etaInput = 10;
  etaPresetValue = '10';
  useCustomEta = false;
  declineReason = '';
  paymentMethod = '';
  paymentStatus = 'Unpaid';

  actionLoading = signal(false);
  paymentLoading = signal(false);
  showDecline = signal(false);

  onEtaPresetChange(val: string): void {
    if (val === 'custom') {
      this.useCustomEta = true;
    } else {
      this.useCustomEta = false;
      this.etaInput = +val;
    }
  }

  readonly statusTabs = [
    { labelKey: 'orders.tabActive', value: 'active' },
    { labelKey: 'orders.tabDone', value: 'done' },
    { labelKey: 'orders.tabAll', value: 'all' },
  ];

  private activeStatuses = [
    OrderStatus.Created,
    OrderStatus.Accepted,
    OrderStatus.Preparing,
    OrderStatus.Ready,
  ];

  filteredOrders = computed(() => {
    const tab = this.activeTab();
    return this.orders().filter((o) => {
      if (tab === 'active') return this.activeStatuses.includes(o.status as OrderStatus);
      if (tab === 'done')
        return o.status === OrderStatus.Completed || o.status === OrderStatus.Declined;
      return true;
    });
  });

  countForStatus(tab: string): number {
    if (tab === 'active')
      return this.orders().filter((o) => this.activeStatuses.includes(o.status as OrderStatus))
        .length;
    return 0;
  }

  private subs: Subscription[] = [];

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => this.locations.set(locs));
    this.loadOrders();

    this.signalr.configure(environment.apiUrl);
    this.signalr.startConnection().then(() => {
      this.subs.push(
        this.signalr.orderCreated$.subscribe((o) => {
          this.orders.update((list) => [o, ...list]);
          this.toast.info(`New order #${o.id.slice(-6).toUpperCase()}`);
        }),
        this.signalr.orderUpdated$.subscribe((o) => this.upsertOrder(o)),
        this.signalr.orderDeclined$.subscribe((o) => this.upsertOrder(o)),
        this.signalr.paymentUpdated$.subscribe((o) => this.upsertOrder(o)),
      );
    });
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    this.signalr.stopConnection();
  }

  loadOrders(): void {
    this.loading.set(true);
    this.api.getAdminOrders(undefined, this.selectedLocationId || undefined).subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  acceptOrder(order: Order): void {
    this.actionLoading.set(true);
    this.api.acceptOrder(order.id, { etaMinutes: this.etaInput }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.toast.success('Order accepted');
      },
      error: () => {
        this.actionLoading.set(false);
        this.toast.error('Failed to accept order');
      },
    });
  }

  declineOrder(order: Order): void {
    this.actionLoading.set(true);
    this.api.declineOrder(order.id, { reason: this.declineReason }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.showDecline.set(false);
        this.declineReason = '';
        this.toast.success('Order declined');
      },
      error: () => {
        this.actionLoading.set(false);
        this.toast.error('Failed to decline order');
      },
    });
  }

  updateStatus(order: Order, status: OrderStatus): void {
    this.actionLoading.set(true);
    this.api.updateOrderStatus(order.id, { status }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.toast.success(`Status: ${status}`);
      },
      error: () => {
        this.actionLoading.set(false);
        this.toast.error('Failed to update status');
      },
    });
  }

  savePayment(order: Order): void {
    this.paymentLoading.set(true);
    this.api
      .updateOrderPayment(order.id, {
        paymentStatus: this.paymentStatus as any,
        paymentMethod: this.paymentMethod as any,
      })
      .subscribe({
        next: (o) => {
          this.upsertOrder(o);
          this.paymentLoading.set(false);
          this.toast.success('Payment updated');
        },
        error: () => {
          this.paymentLoading.set(false);
          this.toast.error('Failed to update payment');
        },
      });
  }

  private upsertOrder(updated: Order): void {
    this.orders.update((list) => {
      const idx = list.findIndex((o) => o.id === updated.id);
      return idx >= 0 ? list.map((o) => (o.id === updated.id ? updated : o)) : [updated, ...list];
    });
    if (this.selectedOrder()?.id === updated.id) this.selectedOrder.set(updated);
  }
}
