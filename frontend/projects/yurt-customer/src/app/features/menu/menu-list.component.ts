import { Component, inject, OnInit, signal, computed, effect } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService } from 'shared-api';
import { MenuItem, MenuCategory, MenuTopping, OrderItemToppingInput, Promotion } from 'shared-models';
import { SkeletonCardComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { LocationService } from '../../core/location.service';

@Component({
  selector: 'app-menu-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SkeletonCardComponent, Currency2Pipe, TranslatePipe],
  templateUrl: './menu-list.component.html',
  styleUrl: './menu-list.component.css',
})
export class MenuListComponent implements OnInit {
  private api = inject(YurtApiService);
  private router = inject(Router);
  readonly cart = inject(CartService);
  private toast = inject(ToastService);
  readonly auth = inject(AuthStateService);
  readonly langService = inject(LangService);
  private locationSvc = inject(LocationService);
  readonly locationName = this.locationSvc.locationName;

  loading = signal(true);
  categories = signal<MenuCategory[]>([]);
  allItems = signal<MenuItem[]>([]);
  promotions = signal<Promotion[]>([]);
  selectedCategoryId = signal<string | null>(null);
  search = '';
  searchHistory = signal<string[]>(this.loadSearchHistory());
  showHistory = signal(false);
  favoritedIds = signal<Set<string>>(new Set());
  failedImageIds = signal<Set<string>>(new Set());

  onImageError(id: string): void {
    this.failedImageIds.update((s) => new Set([...s, id]));
  }

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
    const hour = (new Date().getUTCHours() + 5) % 24;
    const key = hour < 12 ? 'greeting.morning' : hour < 17 ? 'greeting.afternoon' : 'greeting.evening';
    const time = this.langService.t(key);
    return name ? `${time}, ${name}` : time;
  }

  readonly cartCount = this.cart.count;
  readonly cartTotal = this.cart.total;

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      this.locationSvc.locationId(); // track location changes
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

  loadItems(search?: string, lang?: string): void {
    this.loading.set(true);
    const locationId = this.locationSvc.locationId() || undefined;
    this.api.getMenuItems(this.selectedCategoryId() ?? undefined, search, lang ?? this.langService.lang(), locationId).subscribe({
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
    if (this.search.trim()) this.saveSearchHistory(this.search.trim());
    this.showHistory.set(false);
    this.loadItems(this.search || undefined, this.langService.lang());
  }

  onSearchFocus(): void {
    this.showHistory.set(true);
  }

  onSearchBlur(): void {
    setTimeout(() => this.showHistory.set(false), 200);
  }

  selectHistory(term: string): void {
    this.search = term;
    this.showHistory.set(false);
    this.loadItems(term, this.langService.lang());
  }

  removeHistory(term: string, event: Event): void {
    event.stopPropagation();
    const updated = this.searchHistory().filter((h) => h !== term);
    this.searchHistory.set(updated);
    localStorage.setItem('yurt_search_history', JSON.stringify(updated));
  }

  private loadSearchHistory(): string[] {
    try {
      return JSON.parse(localStorage.getItem('yurt_search_history') ?? '[]');
    } catch {
      return [];
    }
  }

  private saveSearchHistory(term: string): void {
    const history = [term, ...this.searchHistory().filter((h) => h !== term)].slice(0, 5);
    this.searchHistory.set(history);
    localStorage.setItem('yurt_search_history', JSON.stringify(history));
  }

  addToCart(item: MenuItem): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/auth/login']);
      return;
    }
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
