import { Component, inject, OnInit, signal, computed, effect } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService } from 'shared-api';
import { MenuItem, MenuCategory, MenuTopping, Order, OrderItemToppingInput, Promotion } from 'shared-models';
import { SkeletonCardComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-menu-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SkeletonCardComponent, Currency2Pipe, TranslatePipe],
  templateUrl: './menu-list.component.html',
  styleUrl: './menu-list.component.css',
})
export class MenuListComponent implements OnInit {
  private api = inject(YurtApiService);
  readonly cart = inject(CartService);
  private toast = inject(ToastService);
  readonly auth = inject(AuthStateService);
  readonly langService = inject(LangService);

  loading = signal(true);
  categories = signal<MenuCategory[]>([]);
  allItems = signal<MenuItem[]>([]);
  promotions = signal<Promotion[]>([]);
  selectedCategoryId = signal<string | null>(null);
  search = '';
  locationName = localStorage.getItem('yurt_location_name') ?? '';
  lastOrder = signal<Order | null>(null);
  favoritedIds = signal<Set<string>>(new Set());

  private static readonly CATEGORY_EMOJIS: Record<string, string> = {
    coffee: '☕', кофе: '☕', қофе: '☕', 'cold drinks': '🧊',
    'холодные напитки': '🧊', 'суық сусындар': '🧊',
    food: '🥐', еда: '🥐', тағам: '🥐',
    desserts: '🍰', десерты: '🍰', десерттер: '🍰',
    tea: '🍵', чай: '🍵', шай: '🍵',
    snacks: '🍿', снеки: '🍿', снектер: '🍿',
    smoothies: '🥤', смузи: '🥤',
    breakfast: '🥞', завтрак: '🥞', 'таңғы ас': '🥞',
  };

  categoryEmoji(name: string): string {
    return MenuListComponent.CATEGORY_EMOJIS[name.toLowerCase()] ?? '';
  }

  get greeting(): string {
    const name = this.auth.currentUser?.displayName;
    const hour = new Date().getHours();
    const key = hour < 12 ? 'greeting.morning' : hour < 17 ? 'greeting.afternoon' : 'greeting.evening';
    const time = this.langService.t(key);
    return name ? `${time}, ${name}` : time;
  }

  readonly cartCount = this.cart.count;
  readonly cartTotal = this.cart.total;

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      this.api.getCategories(lang).subscribe((cats) => this.categories.set(cats));
      this.api.getActivePromotions().subscribe((promos) => this.promotions.set(promos));
      this.loadItems(this.search || undefined, lang);
    });
  }

  readonly filteredItems = computed(() => {
    let items = this.allItems();
    if (this.selectedCategoryId())
      items = items.filter((i) => i.categoryId === this.selectedCategoryId());
    return items;
  });

  // Topping modal state
  toppingModalItem = signal<MenuItem | null>(null);
  selectedToppingIds = signal<Set<string>>(new Set());

  get toppingModalToppings(): MenuTopping[] {
    return this.toppingModalItem()?.availableToppings ?? [];
  }

  /** Toppings split into named radio-groups and a free-pick extras list. */
  get groupedToppings(): { label: string; toppings: MenuTopping[] }[] {
    const all = this.toppingModalToppings;
    const groupMap = new Map<string, MenuTopping[]>();
    const extras: MenuTopping[] = [];

    for (const t of all) {
      if (t.group) {
        if (!groupMap.has(t.group)) groupMap.set(t.group, []);
        groupMap.get(t.group)!.push(t);
      } else {
        extras.push(t);
      }
    }

    const result: { label: string; toppings: MenuTopping[] }[] = [];
    const groupLabels: Record<string, string> = { milk: 'Milk', syrup: 'Syrup' };
    groupMap.forEach((toppings, key) =>
      result.push({ label: groupLabels[key] ?? key, toppings }),
    );
    if (extras.length) result.push({ label: 'Extras', toppings: extras });
    return result;
  }

  ngOnInit(): void {
    this.api.getOrderHistory().subscribe({
      next: (orders) => { if (orders.length) this.lastOrder.set(orders[0]); },
      error: () => {},
    });
    this.api.getFavorites().subscribe({
      next: (favs) => this.favoritedIds.set(new Set(favs.map((f) => f.id))),
      error: () => {},
    });
  }

  isFav(id: string): boolean {
    return this.favoritedIds().has(id);
  }

  toggleFav(item: MenuItem, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    if (this.isFav(item.id)) {
      this.api.removeFavorite(item.id).subscribe(() => {
        this.favoritedIds.update((s) => { const n = new Set(s); n.delete(item.id); return n; });
        this.toast.info('Removed from favorites');
      });
    } else {
      this.api.addFavorite(item.id).subscribe(() => {
        this.favoritedIds.update((s) => { const n = new Set(s); n.add(item.id); return n; });
        this.toast.success('Added to favorites ♥');
      });
    }
  }

  orderAgain(order: Order): void {
    const menuItemMap = new Map(this.allItems().map((i) => [i.id, i]));
    for (const item of order.items) {
      this.cart.addItem({
        menuItemId: item.menuItemId,
        name: item.menuItemName,
        price: item.unitPrice,
        quantity: item.quantity,
        imageUrl: menuItemMap.get(item.menuItemId)?.imageUrl,
        selectedToppings: item.toppings?.map((t) => ({
          toppingId: t.toppingId,
          toppingName: t.toppingName,
          price: t.price,
        })),
      });
    }
    this.toast.success('Last order added to cart!');
  }

  loadItems(search?: string, lang?: string): void {
    this.loading.set(true);
    this.api.getMenuItems(this.selectedCategoryId() ?? undefined, search, lang ?? this.langService.lang()).subscribe({
      next: (items) => {
        this.allItems.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load menu.');
      },
    });
  }

  selectCategory(id: string | null): void {
    this.selectedCategoryId.set(id);
    this.loadItems(this.search || undefined, this.langService.lang());
  }

  onSearch(): void {
    this.loadItems(this.search || undefined, this.langService.lang());
  }

  addToCart(item: MenuItem): void {
    if (item.availableToppings?.length) {
      this.toppingModalItem.set(item);
      this.selectedToppingIds.set(new Set());
    } else {
      this.commitAddToCart(item, []);
    }
  }

  isToppingSelected(id: string): boolean {
    return this.selectedToppingIds().has(id);
  }

  toggleTopping(topping: MenuTopping): void {
    this.selectedToppingIds.update((set) => {
      const next = new Set(set);
      if (next.has(topping.id)) {
        next.delete(topping.id);
      } else {
        // For grouped toppings: deselect any sibling already selected
        if (topping.group) {
          for (const t of this.toppingModalToppings) {
            if (t.group === topping.group) next.delete(t.id);
          }
        }
        next.add(topping.id);
      }
      return next;
    });
  }

  confirmToppings(): void {
    const item = this.toppingModalItem();
    if (!item) return;
    const toppings = (item.availableToppings ?? [])
      .filter((t) => this.selectedToppingIds().has(t.id))
      .map<OrderItemToppingInput>((t) => ({ toppingId: t.id, toppingName: t.name, price: t.price }));
    this.commitAddToCart(item, toppings);
    this.toppingModalItem.set(null);
  }

  dismissToppingModal(): void {
    this.toppingModalItem.set(null);
  }

  private commitAddToCart(item: MenuItem, toppings: OrderItemToppingInput[]): void {
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: item.price,
      quantity: 1,
      imageUrl: item.imageUrl,
      selectedToppings: toppings.length ? toppings : undefined,
    });
    this.toast.success(`${item.name} added to cart`);
  }

  catChipClass(active: boolean): string {
    return active ? 'bg-amber-500 text-white' : 'bg-white text-stone-600 border border-stone-200';
  }
}
