import { CommonModule } from '@angular/common';
import { Component, inject, Input, OnDestroy, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { SignalrService, YurtApiService } from 'shared-api';
import {
  CreatePaymentRequest,
  Order,
  OrderStatus,
  PaymentInvoiceResponse,
  PaymentProvider,
  PaymentStatus,
  SandboxPaymentBehavior,
} from 'shared-models';
import {
  BadgeComponent,
  Currency2Pipe,
  OrderStatusColorPipe,
  OrderStatusLabelPipe,
  ToastService,
} from 'shared-ui';
import { environment } from '../../../environments/environment';
import { PaymentQrCodeComponent } from './payment-qr-code.component';
import { PaymentStatusComponent } from './payment-status.component';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    BadgeComponent,
    OrderStatusLabelPipe,
    OrderStatusColorPipe,
    Currency2Pipe,
    PaymentQrCodeComponent,
    PaymentStatusComponent,
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
  readonly PaymentStatus = PaymentStatus;
  readonly PaymentProvider = PaymentProvider;

  order = signal<Order | null>(null);
  payment = signal<PaymentInvoiceResponse | null>(null);
  paymentLoading = signal(false);
  paymentError = signal<string | null>(null);
  loading = signal(true);
  showReceipt = signal(false);
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

  canPay(order: Order | null): boolean {
    return (
      !!order &&
      order.paymentStatus === PaymentStatus.Unpaid &&
      order.status === OrderStatus.Created
    );
  }

  async startPayment(): Promise<void> {
    const order = this.order();
    if (!order) {
      this.toast.error('Order unavailable.');
      return;
    }

    this.paymentLoading.set(true);
    this.paymentError.set(null);

    const request: CreatePaymentRequest = {
      orderId: order.id,
      provider: PaymentProvider.KaspiSandbox,
      sandboxBehavior: SandboxPaymentBehavior.Default,
    };

    this.api.createPayment(request).subscribe({
      next: (payment) => {
        this.payment.set(payment);
        this.paymentLoading.set(false);
        this.toast.success('Kaspi invoice created. Scan the QR code to pay.');
      },
      error: (err) => {
        this.paymentLoading.set(false);
        this.paymentError.set(err.error?.title ?? 'Failed to create payment.');
      },
    });
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
