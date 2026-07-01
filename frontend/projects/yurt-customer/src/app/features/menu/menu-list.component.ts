import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStateService, YurtApiService } from 'shared-api';
import {
  MenuCategory,
  MenuItem,
  MenuItemVariant,
  MenuTopping,
  OrderItemToppingInput,
  Promotion,
} from 'shared-models';
import { Currency2Pipe, SkeletonCardComponent, ToastService } from 'shared-ui';
import { LangService } from '../../core/lang.service';
import { LocationService } from '../../core/location.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';
import { CartService } from '../cart/cart.service';

@Component({
  selector: 'app-menu-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SkeletonCardComponent,
    Currency2Pipe,
    TranslatePipe,
    PullToRefreshDirective,
  ],
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
    coffee: '☕',
    кофе: '☕',
    қофе: '☕',
    'cold drinks': '🧊',
    'холодные напитки': '🧊',
    'суық сусындар': '🧊',
    food: '🥐',
    еда: '🥐',
    тағам: '🥐',
    desserts: '🍰',
    десерты: '🍰',
    десерттер: '🍰',
    tea: '🍵',
    чай: '🍵',
    шай: '🍵',
    snacks: '🍿',
    снеки: '🍿',
    снектер: '🍿',
    smoothies: '🥤',
    смузи: '🥤',
    breakfast: '🥞',
    завтрак: '🥞',
    'таңғы ас': '🥞',
  };

  categoryEmoji(name: string): string {
    return MenuListComponent.CATEGORY_EMOJIS[name.toLowerCase()] ?? '';
  }

  get greeting(): string {
    const name = this.auth.currentUser?.displayName;
    const hour = (new Date().getUTCHours() + 5) % 24;
    const key =
      hour < 12 ? 'greeting.morning' : hour < 17 ? 'greeting.afternoon' : 'greeting.evening';
    const time = this.langService.t(key);
    return name ? `${time}, ${name}` : time;
  }

  readonly cartCount = this.cart.count;
  readonly cartTotal = this.cart.total;

  searchTerm = signal('');

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      const locationId = this.locationSvc.locationId() || undefined;
      this.api.getCategories(lang, locationId).subscribe((cats) => this.categories.set(cats));
      this.api.getActivePromotions().subscribe((promos) => this.promotions.set(promos));
      this.loadItems(lang);
    });
  }

  readonly filteredItems = computed(() => {
    const q = this.searchTerm().toLowerCase().trim();
    let items = this.allItems();
    if (q) {
      items = items.filter(
        (i) =>
          i.name.toLowerCase().includes(q) ||
          (i.nameRu ?? '').toLowerCase().includes(q) ||
          (i.nameKk ?? '').toLowerCase().includes(q) ||
          i.categoryName.toLowerCase().includes(q),
      );
    } else if (this.selectedCategoryId()) {
      items = items.filter((i) => i.categoryId === this.selectedCategoryId());
    }
    return items;
  });

  // Topping/variant modal state
  toppingModalItem = signal<MenuItem | null>(null);
  selectedToppingIds = signal<Set<string>>(new Set());
  selectedVariant = signal<MenuItemVariant | null>(null);

  get modalTotal(): number {
    const item = this.toppingModalItem();
    if (!item) return 0;
    const base = this.selectedVariant()?.price ?? item.price;
    const extras = (item.availableToppings ?? [])
      .filter((t) => this.selectedToppingIds().has(t.id))
      .reduce((sum, t) => sum + t.price, 0);
    return base + extras;
  }

  private sheetTouchStartY = 0;

  onSheetTouchStart(e: TouchEvent): void {
    this.sheetTouchStartY = e.touches[0].clientY;
  }

  onSheetTouchEnd(e: TouchEvent): void {
    const dy = e.changedTouches[0].clientY - this.sheetTouchStartY;
    if (dy > 80) this.dismissToppingModal();
  }

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
    groupMap.forEach((toppings, key) =>
      result.push({ label: this.langService.t(`topping.${key}`), toppings }),
    );
    if (extras.length)
      result.push({ label: this.langService.t('topping.extras'), toppings: extras });
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
        this.favoritedIds.update((s) => {
          const n = new Set(s);
          n.delete(item.id);
          return n;
        });
        this.toast.info('Removed from favorites');
      });
    } else {
      this.api.addFavorite(item.id).subscribe(() => {
        this.favoritedIds.update((s) => {
          const n = new Set(s);
          n.add(item.id);
          return n;
        });
        this.toast.success('Added to favorites ♥');
      });
    }
  }

  loadItems(lang?: string): void {
    this.loading.set(true);
    const locationId = this.locationSvc.locationId() || undefined;
    this.api
      .getMenuItems(undefined, undefined, lang ?? this.langService.lang(), locationId)
      .subscribe({
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
    this.search = '';
    this.searchTerm.set('');
  }

  onSearch(): void {
    if (this.search.trim()) this.saveSearchHistory(this.search.trim());
    this.searchTerm.set(this.search);
    this.showHistory.set(false);
  }

  onSearchFocus(): void {
    this.showHistory.set(true);
  }

  onSearchBlur(): void {
    setTimeout(() => this.showHistory.set(false), 200);
  }

  selectHistory(term: string): void {
    this.search = term;
    this.searchTerm.set(term);
    this.showHistory.set(false);
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

  addToCart(item: MenuItem, event: MouseEvent): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/auth/login']);
      return;
    }
    const needsModal =
      (item.variants?.length ?? 0) > 0 || (item.availableToppings?.length ?? 0) > 0;
    if (needsModal) {
      this.toppingModalItem.set(item);
      this.selectedToppingIds.set(new Set());
      const defaultVariant = item.variants?.find((v) => v.isDefault) ?? item.variants?.[0] ?? null;
      this.selectedVariant.set(defaultVariant);
    } else {
      this.flyToCart(event, item);
      this.commitAddToCart(item, []);
    }
  }

  private flyToCart(event: MouseEvent, item: MenuItem): void {
    const btn = event.currentTarget as HTMLElement;
    const from = btn.getBoundingClientRect();

    const size = 44;
    const startX = from.left + from.width / 2 - size / 2;
    const startY = from.top + from.height / 2 - size / 2;

    // Cart button may not be in DOM yet (empty cart — it's behind @if cartCount() > 0)
    const cartEl = document.querySelector('.cart-fly-target') as HTMLElement | null;
    let endX: number, endY: number;
    if (cartEl) {
      const to = cartEl.getBoundingClientRect();
      endX = to.left + to.width / 2 - size / 2;
      endY = to.top + to.height / 2 - size / 2;
    } else {
      // Fallback: match the fixed position style="bottom: calc(5rem + env(safe-area-inset-top))" right-4
      endX = window.innerWidth - 16 - 28 - size / 2;
      endY = window.innerHeight - 80 - 28 - size / 2;
    }

    const dx = endX - startX;
    const dy = endY - startY;

    const ghost = document.createElement('div');
    ghost.style.cssText = `position:fixed;width:${size}px;height:${size}px;border-radius:50%;top:${startY}px;left:${startX}px;z-index:9999;pointer-events:none;overflow:hidden;background:#f59e0b;box-shadow:0 4px 14px rgba(245,158,11,0.55);`;
    if (item.imageUrl) {
      const img = document.createElement('img');
      img.src = item.imageUrl;
      img.style.cssText = 'width:100%;height:100%;object-fit:cover;';
      ghost.appendChild(img);
    }
    document.body.appendChild(ghost);

    ghost.animate(
      [
        { transform: 'translate(0,0) scale(1)', opacity: '1' },
        {
          transform: `translate(${dx * 0.4}px,${dy * 0.15 - 70}px) scale(0.65)`,
          opacity: '0.85',
          offset: 0.45,
        },
        { transform: `translate(${dx}px,${dy}px) scale(0.1)`, opacity: '0' },
      ],
      { duration: 920, easing: 'cubic-bezier(0.4,0,0.7,1)', fill: 'forwards' },
    ).onfinish = () => ghost.remove();
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

  confirmToppings(event: MouseEvent): void {
    const item = this.toppingModalItem();
    if (!item) return;
    const toppings = (item.availableToppings ?? [])
      .filter((t) => this.selectedToppingIds().has(t.id))
      .map<OrderItemToppingInput>((t) => ({
        toppingId: t.id,
        toppingName: t.name,
        price: t.price,
      }));
    this.flyToCart(event, item);
    this.toppingModalItem.set(null);
    this.commitAddToCart(item, toppings, this.selectedVariant());
  }

  dismissToppingModal(): void {
    this.toppingModalItem.set(null);
  }

  private commitAddToCart(
    item: MenuItem,
    toppings: OrderItemToppingInput[],
    variant?: MenuItemVariant | null,
  ): void {
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: variant?.price ?? item.price,
      quantity: 1,
      imageUrl: item.imageUrl,
      selectedToppings: toppings.length ? toppings : undefined,
      variantId: variant?.id,
      variantLabel: variant?.label,
    });
    this.toast.success(`${item.name} added to cart`);
  }

  catChipClass(active: boolean): string {
    return active ? 'bg-amber-500 text-white' : 'bg-white text-stone-600 border border-stone-200';
  }
}
