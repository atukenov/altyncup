import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService } from 'shared-api';
import { CartItem, PaymentMethod } from 'shared-models';
import { CartService } from './cart.service';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { TranslatePipe } from '../../core/translate.pipe';
import { LocationService } from '../../core/location.service';
import { LangService } from '../../core/lang.service';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, Currency2Pipe, TranslatePipe],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.css',
})
export class CartComponent {
  readonly cart = inject(CartService);
  readonly langService = inject(LangService);
  private api = inject(YurtApiService);
  private router = inject(Router);
  private toast = inject(ToastService);

  private auth = inject(AuthStateService);
  private locationSvc = inject(LocationService);
  readonly locationName = this.locationSvc.locationName;
  readonly PaymentMethod = PaymentMethod;

  loading = signal(false);
  selectedPaymentMethod = signal<PaymentMethod | null>(null);
  expandedNoteKeys = signal<Set<string>>(new Set());
  expandedItemKeys = signal<Set<string>>(new Set());

  promoCodeInput = signal('');
  promoLoading = signal(false);
  promoError = signal<string | null>(null);
  showConfirm = signal(false);

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

  toggleItem(key: string): void {
    this.expandedItemKeys.update((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  isItemExpanded(key: string): boolean {
    return this.expandedItemKeys().has(key);
  }

  itemLineTotal(item: CartItem): number {
    const toppings = item.selectedToppings?.reduce((s, t) => s + t.price, 0) ?? 0;
    return (item.price + toppings) * item.quantity;
  }

  goToMenu(): void {
    this.router.navigate(['/menu']);
  }

  applyPromoCode(): void {
    const code = this.promoCodeInput().trim();
    if (!code) return;
    this.promoError.set(null);
    this.promoLoading.set(true);
    this.api.validateDiscountCode(code, this.cart.subtotal()).subscribe({
      next: (res) => {
        this.promoLoading.set(false);
        if (res.isValid) {
          this.cart.applyDiscount(code, res);
          this.toast.success(`${this.langService.t('cart.promoCode')}: ${res.description}`);
        } else {
          const msg = res.message;
          const key = msg.includes('not found') ? 'cart.promoNotFound'
            : msg.includes('not yet active') ? 'cart.promoNotActive'
            : msg.includes('expired') ? 'cart.promoExpired'
            : msg.includes('usage limit') ? 'cart.promoMaxUses'
            : msg.includes('Minimum order') ? 'cart.promoMinOrder'
            : null;
          this.promoError.set(key ? this.langService.t(key) : msg);
        }
      },
      error: () => {
        this.promoLoading.set(false);
        this.promoError.set(this.langService.t('cart.promoNetworkError'));
      },
    });
  }

  removePromoCode(): void {
    this.cart.clearDiscount();
    this.promoCodeInput.set('');
  }

  checkout(): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/auth/login']);
      return;
    }
    const locationId = this.locationSvc.locationId();
    if (!locationId) {
      this.toast.warning('Please select a location first.');
      this.router.navigate(['/locations']);
      return;
    }
    if (!this.cart.items().length) {
      this.toast.warning('Cart is empty.');
      return;
    }

    const paymentMethod = this.selectedPaymentMethod();
    if (!paymentMethod) {
      this.toast.warning('Select a payment method to continue.');
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
        variantId: i.variantId,
      }));

    const discountCode = this.cart.appliedDiscount()?.code;

    this.api.createOrder({ locationId, items, paymentMethod, discountCode }).subscribe({
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
