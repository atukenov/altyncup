import { Component, inject, Input, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { YurtApiService, SignalrService } from 'shared-api';
import { Order, OrderStatus } from 'shared-models';
import {
  BadgeComponent,
  ToastService,
  OrderStatusLabelPipe,
  OrderStatusColorPipe,
  Currency2Pipe,
} from 'shared-ui';
import { environment } from '../../../environments/environment';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    BadgeComponent,
    OrderStatusLabelPipe,
    OrderStatusColorPipe,
    Currency2Pipe,
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

  readonly OrderStatus = OrderStatus;
  order = signal<Order | null>(null);
  loading = signal(true);
  private subs: Subscription[] = [];

  readonly timelineSteps = [
    { status: OrderStatus.Created, label: 'Order Placed' },
    { status: OrderStatus.Accepted, label: 'Accepted by café' },
    { status: OrderStatus.Preparing, label: 'Preparing your order' },
    { status: OrderStatus.Ready, label: 'Ready for Pickup!' },
    { status: OrderStatus.Completed, label: 'Completed' },
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
          }
        }),
        this.signalr.orderDeclined$.subscribe((o) => {
          if (o.id === this.id) {
            this.order.set(o);
            this.toast.error('Order declined');
          }
        }),
        this.signalr.paymentUpdated$.subscribe((o) => {
          if (o.id === this.id) this.order.set(o);
        })
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