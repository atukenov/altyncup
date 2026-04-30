import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuCategory } from 'shared-models';
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
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: item.price,
      quantity: 1,
      imageUrl: item.imageUrl,
    });
    this.toast.success(`${item.name} added to cart`);
  }

  catChipClass(active: boolean): string {
    return active ? 'bg-amber-500 text-white' : 'bg-white text-stone-600 border border-stone-200';
  }
}