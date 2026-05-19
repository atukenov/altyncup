import { inject, Injectable } from '@angular/core';
import { Order, OrderStatus } from 'shared-models';
import { NotificationService } from './notification.service';
import { SignalrService } from './signalr.service';

@Injectable({ providedIn: 'root' })
export class OrderNotificationService {
  private signalr = inject(SignalrService);
  private notifications = inject(NotificationService);
  private lastNotifiedOrders = new Map<string, OrderStatus>();

  initialize(): void {
    // Listen for order status updates
    this.signalr.orderUpdated$.subscribe((order: Order) => {
      this.handleOrderUpdate(order);
    });

    // Listen for order creation
    this.signalr.orderCreated$.subscribe((order: Order) => {
      this.sendOrderNotification(
        'New Order Created',
        `Your order has been received and is being prepared.`,
        order,
        'created',
      );
    });

    // Listen for payment updates
    this.signalr.paymentUpdated$.subscribe((order: Order) => {
      const statusMessage = this.getStatusMessage(order.status);
      this.sendOrderNotification(
        'Payment Updated',
        `Payment status: ${statusMessage}`,
        order,
        'payment',
      );
    });

    // Listen for order declination
    this.signalr.orderDeclined$.subscribe((order: Order) => {
      this.sendOrderNotification(
        'Order Declined',
        'Unfortunately, your order has been declined. Please contact support.',
        order,
        'declined',
      );
    });
  }

  private handleOrderUpdate(order: Order): void {
    const lastStatus = this.lastNotifiedOrders.get(order.id);

    // Only notify if status actually changed
    if (lastStatus === order.status) {
      return;
    }

    this.lastNotifiedOrders.set(order.id, order.status);

    // Skip initial status to avoid duplicate notifications
    if (lastStatus === undefined) {
      return;
    }

    const statusMessage = this.getStatusMessage(order.status);
    this.sendOrderNotification(
      'Order Status Updated',
      `Your order status has changed to: ${statusMessage}`,
      order,
      'status-update',
    );
  }

  private sendOrderNotification(title: string, body: string, order: Order, type: string): void {
    this.notifications
      .sendNotification({
        title,
        body,
        tag: `order-${order.id}`,
        largeBody: `Order ID: ${order.id}\n${body}`,
      })
      .catch((error) => {
        console.error('Failed to send order notification:', error);
      });
  }

  private getStatusMessage(status: OrderStatus): string {
    const statusMessages: Record<OrderStatus, string> = {
      [OrderStatus.Pending]: 'Pending',
      [OrderStatus.Confirmed]: 'Confirmed',
      [OrderStatus.Preparing]: 'Being Prepared',
      [OrderStatus.Ready]: 'Ready for Pickup',
      [OrderStatus.Completed]: 'Completed',
      [OrderStatus.Cancelled]: 'Cancelled',
      [OrderStatus.Declined]: 'Declined',
    };

    return statusMessages[status] || status;
  }
}
