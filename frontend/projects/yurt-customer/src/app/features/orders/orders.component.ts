import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Order, OrderStatus } from 'shared-models';
import {
  BadgeComponent,
  ToastService,
  OrderStatusLabelPipe,
  OrderStatusColorPipe,
  Currency2Pipe,
} from 'shared-ui';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    BadgeComponent,
    OrderStatusLabelPipe,
    OrderStatusColorPipe,
    Currency2Pipe,
    TranslatePipe,
  ],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.css',
})
export class OrdersComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  loading = signal(true);
  activeOrders = signal<Order[]>([]);
  historyOrders = signal<Order[]>([]);
  declinedOrders = signal<Order[]>([]);
  activeTab = signal(0);

  readonly tabs = [
    { id: 0, labelKey: 'orders.tabActive' },
    { id: 1, labelKey: 'orders.tabHistory' },
    { id: 2, labelKey: 'orders.tabDeclined' },
  ];

  get visibleOrders(): () => Order[] {
    return () => {
      switch (this.activeTab()) {
        case 0:
          return this.activeOrders();
        case 1:
          return this.historyOrders();
        case 2:
          return this.declinedOrders();
        default:
          return [];
      }
    };
  }

  ngOnInit(): void {
    Promise.all([
      this.api.getActiveOrders().toPromise(),
      this.api.getOrderHistory().toPromise(),
      this.api.getDeclinedOrders().toPromise(),
    ])
      .then(([active, history, declined]) => {
        this.activeOrders.set(active ?? []);
        this.historyOrders.set(history ?? []);
        this.declinedOrders.set(declined ?? []);
        this.loading.set(false);
      })
      .catch(() => {
        this.loading.set(false);
        this.toast.error('Failed to load orders.');
      });
  }
}