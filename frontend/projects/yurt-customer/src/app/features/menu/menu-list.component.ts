import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuCategory, MenuTopping, OrderItemToppingInput } from 'shared-models';
import { SkeletonCardComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { CartService } from '../cart/cart.service';

@Component({
  selector: 'app-menu-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SkeletonCardComponent, Currency2Pipe],
  templateUrl: './menu-list.component.html',
  styleUrl: './menu-list.component.css',
})
export class MenuListComponent implements OnInit {
  private api = inject(YurtApiService);
  private cart = inject(CartService);
  private toast = inject(ToastService);

  loading = signal(true);
  categories = signal<MenuCategory[]>([]);
  allItems = signal<MenuItem[]>([]);
  selectedCategoryId = signal<string | null>(null);
  search = '';
  locationName = localStorage.getItem('yurt_location_name') ?? '';

  readonly cartCount = this.cart.count;
  readonly cartTotal = this.cart.total;

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

  ngOnInit(): void {
    this.api.getCategories().subscribe((cats) => this.categories.set(cats));
    this.loadItems();
  }

  loadItems(search?: string): void {
    this.loading.set(true);
    this.api.getMenuItems(this.selectedCategoryId() ?? undefined, search).subscribe({
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
    this.loadItems(this.search || undefined);
  }

  onSearch(): void {
    this.loadItems(this.search || undefined);
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

  toggleTopping(id: string): void {
    this.selectedToppingIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
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
