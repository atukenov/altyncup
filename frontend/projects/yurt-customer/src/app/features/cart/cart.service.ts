import { Injectable, signal, computed } from '@angular/core';
import { CartItem } from 'shared-models';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly CART_KEY = 'yurt_cart';
  readonly items = signal<CartItem[]>(this.loadCart());

  readonly total = computed(() =>
    this.items().reduce((sum, i) => {
      const toppingsPrice = i.selectedToppings?.reduce((s, t) => s + t.price, 0) ?? 0;
      return sum + (i.price + toppingsPrice) * i.quantity;
    }, 0),
  );

  readonly count = computed(() => this.items().reduce((sum, i) => sum + i.quantity, 0));

  addItem(item: CartItem): void {
    this.items.update((cart) => {
      const existing = cart.find((c) => c.menuItemId === item.menuItemId);
      if (existing) {
        return cart.map((c) =>
          c.menuItemId === item.menuItemId ? { ...c, quantity: c.quantity + item.quantity } : c,
        );
      }
      return [...cart, item];
    });
    this.save();
  }

  removeItem(menuItemId: string): void {
    this.items.update((cart) => cart.filter((i) => i.menuItemId !== menuItemId));
    this.save();
  }

  updateQty(menuItemId: string, qty: number): void {
    if (qty <= 0) {
      this.removeItem(menuItemId);
      return;
    }
    this.items.update((cart) =>
      cart.map((i) => (i.menuItemId === menuItemId ? { ...i, quantity: qty } : i)),
    );
    this.save();
  }

  clear(): void {
    this.items.set([]);
    this.save();
  }

  private save(): void {
    localStorage.setItem(this.CART_KEY, JSON.stringify(this.items()));
  }

  private loadCart(): CartItem[] {
    try {
      const raw = localStorage.getItem(this.CART_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }
}
