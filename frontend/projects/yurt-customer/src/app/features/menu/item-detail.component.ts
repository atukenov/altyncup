import { Component, inject, OnInit, signal, computed, Input, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuTopping, OrderItemToppingInput } from 'shared-models';
import { ToastService, Currency2Pipe } from 'shared-ui';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-item-detail',
  standalone: true,
  imports: [CommonModule, Currency2Pipe, TranslatePipe],
  templateUrl: './item-detail.component.html',
  styleUrl: './item-detail.component.css',
})
export class ItemDetailComponent implements OnInit {
  @Input() id!: string;

  private api = inject(YurtApiService);
  private cart = inject(CartService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private langService = inject(LangService);

  item = signal<MenuItem | null>(null);
  loading = signal(true);
  quantity = signal(1);
  isFav = signal(false);
  selectedToppingIds = signal<Set<string>>(new Set());

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      if (this.id) this.loadItem(lang);
    });
  }

  private loadItem(lang: string): void {
    this.loading.set(true);
    this.api.getMenuItem(this.id, lang).subscribe({
      next: (item) => { this.item.set(item); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Failed to load item.'); },
    });
  }

  ngOnInit(): void {
    this.api.getFavorites().subscribe({
      next: (favs) => this.isFav.set(favs.some((f) => f.id === this.id)),
      error: () => {},
    });
  }

  get toppings(): MenuTopping[] {
    return this.item()?.availableToppings ?? [];
  }

  get groupedToppings(): { label: string; toppings: MenuTopping[] }[] {
    const groupMap = new Map<string, MenuTopping[]>();
    const extras: MenuTopping[] = [];
    for (const t of this.toppings) {
      if (t.group) {
        if (!groupMap.has(t.group)) groupMap.set(t.group, []);
        groupMap.get(t.group)!.push(t);
      } else {
        extras.push(t);
      }
    }
    const groupLabels: Record<string, string> = { milk: 'Milk', syrup: 'Syrup' };
    const result: { label: string; toppings: MenuTopping[] }[] = [];
    groupMap.forEach((toppings, key) => result.push({ label: groupLabels[key] ?? key, toppings }));
    if (extras.length) result.push({ label: 'Extras', toppings: extras });
    return result;
  }

  readonly toppingTotal = computed(() => {
    const item = this.item();
    if (!item?.availableToppings) return 0;
    return item.availableToppings
      .filter((t) => this.selectedToppingIds().has(t.id))
      .reduce((sum, t) => sum + t.price, 0);
  });

  readonly lineTotal = computed(() =>
    (this.item()?.price ?? 0 + this.toppingTotal()) * this.quantity()
  );

  isToppingSelected(id: string): boolean {
    return this.selectedToppingIds().has(id);
  }

  toggleTopping(topping: MenuTopping): void {
    this.selectedToppingIds.update((set) => {
      const next = new Set(set);
      if (next.has(topping.id)) {
        next.delete(topping.id);
      } else {
        if (topping.group) {
          for (const t of this.toppings) {
            if (t.group === topping.group) next.delete(t.id);
          }
        }
        next.add(topping.id);
      }
      return next;
    });
  }

  incrementQty(): void { this.quantity.update((q) => q + 1); }
  decrementQty(): void { if (this.quantity() > 1) this.quantity.update((q) => q - 1); }

  addToCart(): void {
    const item = this.item();
    if (!item) return;
    const selectedToppings = (item.availableToppings ?? [])
      .filter((t) => this.selectedToppingIds().has(t.id))
      .map<OrderItemToppingInput>((t) => ({ toppingId: t.id, toppingName: t.name, price: t.price }));
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: item.price,
      quantity: this.quantity(),
      imageUrl: item.imageUrl,
      selectedToppings: selectedToppings.length ? selectedToppings : undefined,
    });
    this.toast.success(`${this.quantity()}x ${item.name} added to cart`);
    this.router.navigate(['/menu']);
  }

  toggleFavorite(): void {
    if (this.isFav()) {
      this.api.removeFavorite(this.id).subscribe(() => {
        this.isFav.set(false);
        this.toast.info('Removed from favorites');
      });
    } else {
      this.api.addFavorite(this.id).subscribe(() => {
        this.isFav.set(true);
        this.toast.success('Added to favorites ♥');
      });
    }
  }

  goBack(): void { this.router.navigate(['/menu']); }
}
