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

  itemKey(item: CartItem): string {
    const toppingIds = (item.selectedToppings ?? []).map((t) => t.toppingId).sort().join(',');
    return toppingIds ? `${item.menuItemId}:${toppingIds}` : item.menuItemId;
  }

  addItem(item: CartItem): void {
    this.items.update((cart) => {
      const key = this.itemKey(item);
      const existing = cart.find((c) => this.itemKey(c) === key);
      if (existing) {
        return cart.map((c) =>
          this.itemKey(c) === key ? { ...c, quantity: c.quantity + item.quantity } : c,
        );
      }
      return [...cart, item];
    });
    this.save();
  }

  removeItem(item: CartItem): void {
    const key = this.itemKey(item);
    this.items.update((cart) => cart.filter((i) => this.itemKey(i) !== key));
    this.save();
  }

  updateQty(item: CartItem, qty: number): void {
    if (qty <= 0) {
      this.removeItem(item);
      return;
    }
    const key = this.itemKey(item);
    this.items.update((cart) =>
      cart.map((i) => (this.itemKey(i) === key ? { ...i, quantity: qty } : i)),
    );
    this.save();
  }

  updateNotes(item: CartItem, notes: string): void {
    const key = this.itemKey(item);
    this.items.update((cart) =>
      cart.map((i) => (this.itemKey(i) === key ? { ...i, notes: notes || undefined } : i)),
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
