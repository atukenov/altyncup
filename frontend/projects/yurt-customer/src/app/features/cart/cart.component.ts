import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { CartService } from './cart.service';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, Currency2Pipe, TranslatePipe],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.css',
})
export class CartComponent {
  readonly cart = inject(CartService);
  private api = inject(YurtApiService);
  private router = inject(Router);
  private toast = inject(ToastService);

  loading = signal(false);
  locationName = localStorage.getItem('yurt_location_name') ?? '';
  expandedNoteKeys = signal<Set<string>>(new Set());

  toggleNote(key: string): void {
    this.expandedNoteKeys.update((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  isNoteExpanded(key: string): boolean {
    return this.expandedNoteKeys().has(key);
  }

  goToMenu(): void {
    this.router.navigate(['/menu']);
  }

  checkout(): void {
    const locationId = localStorage.getItem('yurt_location_id');
    if (!locationId) {
      this.toast.warning('Please select a location first.');
      this.router.navigate(['/locations']);
      return;
    }
    if (!this.cart.items().length) {
      this.toast.warning('Cart is empty.');
      return;
    }

    this.loading.set(true);
    const items = this.cart
      .items()
      .map((i) => ({
        menuItemId: i.menuItemId,
        quantity: i.quantity,
        toppings: i.selectedToppings ?? [],
        notes: i.notes,
      }));

    this.api.createOrder({ locationId, items }).subscribe({
      next: (order) => {
        this.cart.clear();
        this.toast.success('Order placed! ☕');
        this.router.navigate(['/orders', order.id]);
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.error(err.error?.title ?? 'Failed to place order.');
      },
    });
  }
}