import { CommonModule } from '@angular/common';
import { Component, inject, Input, OnDestroy, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NotificationService, SignalrService, YurtApiService } from 'shared-api';
import {
  Order,
  OrderStatus,
  PaymentMethod,
  PaymentStatus,
} from 'shared-models';
import {
  BadgeComponent,
  Currency2Pipe,
  OrderStatusColorPipe,
  OrderStatusLabelPipe,
  ToastService,
} from 'shared-ui';
import { environment } from '../../../environments/environment';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    BadgeComponent,
    OrderStatusLabelPipe,
    OrderStatusColorPipe,
    Currency2Pipe,
    TranslatePipe,
  ],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.css',
})
export class OrderDetailComponent implements OnInit, OnDestroy {
  @Input() id!: string;

  readonly router = inject(Router);
  private api = inject(YurtApiService);
  private signalr = inject(SignalrService);
  private toast = inject(ToastService);
  private notif = inject(NotificationService);

  readonly OrderStatus = OrderStatus;
  readonly PaymentStatus = PaymentStatus;
  readonly PaymentMethod = PaymentMethod;

  order = signal<Order | null>(null);
  loading = signal(true);
  showReceipt = signal(false);
  private subs: Subscription[] = [];

  readonly timelineSteps = [
    { status: OrderStatus.Created, labelKey: 'timeline.Created' },
    { status: OrderStatus.Accepted, labelKey: 'timeline.Accepted' },
    { status: OrderStatus.Preparing, labelKey: 'timeline.Preparing' },
    { status: OrderStatus.Ready, labelKey: 'timeline.Ready' },
    { status: OrderStatus.Completed, labelKey: 'timeline.Completed' },
  ];

  readonly statusOrder = [
    OrderStatus.Created,
    OrderStatus.Accepted,
    OrderStatus.Preparing,
    OrderStatus.Ready,
    OrderStatus.Completed,
  ];

  ngOnInit(): void {
    this.api.getOrder(this.id).subscribe({
      next: (o) => {
        this.order.set(o);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Order not found.');
      },
    });

    this.signalr.configure(environment.apiUrl);
    this.signalr.startConnection().then(() => {
      this.subs.push(
        this.signalr.orderUpdated$.subscribe((o) => {
          if (o.id === this.id) {
            this.order.set(o);
            this.toast.info(`Order ${o.status}`);
            if (localStorage.getItem('yurt_push_enabled') !== 'false') {
              this.notif.sendNotification({
                title: 'Order Update',
                body: `Your order is now ${o.status}`,
                tag: `order-${o.id}`,
              });
            }
          }
        }),
        this.signalr.orderDeclined$.subscribe((o) => {
          if (o.id === this.id) {
            this.order.set(o);
            this.toast.error('Order declined');
            if (localStorage.getItem('yurt_push_enabled') !== 'false') {
              this.notif.sendNotification({
                title: 'Order Declined',
                body: o.declineReason ?? 'Your order was declined.',
                tag: `order-${o.id}`,
              });
            }
          }
        }),
        this.signalr.paymentUpdated$.subscribe((o) => {
          if (o.id === this.id) this.order.set(o);
        }),
      );
    });
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
  }

  isActive(status: OrderStatus): boolean {
    return [
      OrderStatus.Created,
      OrderStatus.Accepted,
      OrderStatus.Preparing,
      OrderStatus.Ready,
    ].includes(status);
  }

  stepIsReached(current: OrderStatus, step: OrderStatus): boolean {
    return this.statusOrder.indexOf(current) >= this.statusOrder.indexOf(step);
  }

  stepDotClass(current: OrderStatus, step: OrderStatus): string {
    return this.stepIsReached(current, step) ? 'bg-amber-500' : 'bg-stone-200';
  }

  statusEmoji(status: OrderStatus): string {
    const map: Record<OrderStatus, string> = {
      [OrderStatus.Created]: '📋',
      [OrderStatus.Accepted]: '✅',
      [OrderStatus.Preparing]: '☕',
      [OrderStatus.Ready]: '🎉',
      [OrderStatus.Completed]: '🌟',
      [OrderStatus.Declined]: '❌',
    };
    return map[status] ?? '📋';
  }
}
