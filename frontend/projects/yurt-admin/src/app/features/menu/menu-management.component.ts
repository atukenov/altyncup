import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuCategory, MenuTopping } from 'shared-models';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';

interface MenuItemForm {
  id?: string;
  name: string;
  description: string;
  price: number;
  categoryId: string;
  imageUrl: string;
  isAvailable: boolean;
}

interface ToppingForm {
  id?: string;
  name: string;
  price: number;
  isAvailable: boolean;
  categoryIds: string[];
  group: string;
}

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent, Currency2Pipe],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.css',
})
export class MenuManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  activeTab = signal<'items' | 'toppings'>('items');

  items = signal<MenuItem[]>([]);
  categories = signal<MenuCategory[]>([]);
  toppings = signal<MenuTopping[]>([]);
  loading = signal(true);
  saving = signal(false);
  showItemDialog = signal(false);
  showCatDialog = signal(false);
  showToppingDialog = signal(false);
  selectedCategoryId = signal('');
  catInput = signal<{ name: string }>({ name: '' });

  itemForm = signal<MenuItemForm>({
    name: '',
    description: '',
    price: 0,
    categoryId: '',
    imageUrl: '',
    isAvailable: true,
  });

  toppingForm = signal<ToppingForm>({
    name: '',
    price: 0,
    isAvailable: true,
    categoryIds: [],
    group: '',
  });

  filteredItems = computed(() => {
    const catId = this.selectedCategoryId();
    return catId ? this.items().filter((i) => i.categoryId === catId) : this.items();
  });

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.api.getCategories().subscribe((cats) => this.categories.set(cats));
    this.api.adminGetMenuItems().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.api.adminGetToppings().subscribe((toppings) => this.toppings.set(toppings));
  }

  categoryName(id: string): string {
    return this.categories().find((c) => c.id === id)?.name ?? '';
  }

  toppingCategoryNames(categoryIds: string[]): string {
    return categoryIds
      .map((id) => this.categoryName(id))
      .filter(Boolean)
      .join(', ') || '—';
  }

  openItemDialog(item?: MenuItem): void {
    this.itemForm.set({
      id: item?.id,
      name: item?.name ?? '',
      description: item?.description ?? '',
      price: item?.price ?? 0,
      categoryId: item?.categoryId ?? this.categories()[0]?.id ?? '',
      imageUrl: item?.imageUrl ?? '',
      isAvailable: item?.isAvailable ?? true,
    });
    this.showItemDialog.set(true);
  }

  patchItemForm(field: keyof MenuItemForm, value: unknown): void {
    this.itemForm.update((f) => ({ ...f, [field]: value }));
  }

  saveItem(): void {
    const f = this.itemForm();
    if (!f.name || !f.price) {
      this.toast.error('Name and price are required');
      return;
    }
    this.saving.set(true);
    const obs = f.id ? this.api.adminUpdateMenuItem(f.id, f) : this.api.adminCreateMenuItem(f);
    obs.subscribe({
      next: (item) => {
        if (f.id) {
          this.items.update((list) => list.map((i) => (i.id === item.id ? item : i)));
        } else {
          this.items.update((list) => [...list, item]);
        }
        this.saving.set(false);
        this.showItemDialog.set(false);
        this.toast.success('Item saved');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Failed to save item');
      },
    });
  }

  deleteItem(item: MenuItem): void {
    if (!confirm(`Delete "${item.name}"?`)) return;
    this.api.adminDeleteMenuItem(item.id).subscribe({
      next: () => {
        this.items.update((list) => list.filter((i) => i.id !== item.id));
        this.toast.success('Item deleted');
      },
      error: () => this.toast.error('Failed to delete item'),
    });
  }

  saveCat(): void {
    const name = this.catInput().name.trim();
    if (!name) return;
    this.saving.set(true);
    this.api.adminCreateCategory({ name }).subscribe({
      next: (cat) => {
        this.categories.update((list) => [...list, cat]);
        this.saving.set(false);
        this.showCatDialog.set(false);
        this.toast.success('Category created');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Failed to create category');
      },
    });
  }

  openToppingDialog(topping?: MenuTopping): void {
    this.toppingForm.set({
      id: topping?.id,
      name: topping?.name ?? '',
      price: topping?.price ?? 0,
      isAvailable: topping?.isAvailable ?? true,
      categoryIds: topping?.categoryIds ? [...topping.categoryIds] : [],
      group: topping?.group ?? '',
    });
    this.showToppingDialog.set(true);
  }

  isToppingCategorySelected(catId: string): boolean {
    return this.toppingForm().categoryIds.includes(catId);
  }

  toggleToppingCategory(catId: string): void {
    this.toppingForm.update((f) => {
      const ids = f.categoryIds.includes(catId)
        ? f.categoryIds.filter((id) => id !== catId)
        : [...f.categoryIds, catId];
      return { ...f, categoryIds: ids };
    });
  }

  patchToppingForm(field: keyof ToppingForm, value: unknown): void {
    this.toppingForm.update((f) => ({ ...f, [field]: value }));
  }

  saveTopping(): void {
    const f = this.toppingForm();
    if (!f.name) {
      this.toast.error('Topping name is required');
      return;
    }
    this.saving.set(true);
    const obs = f.id
      ? this.api.adminUpdateTopping(f.id, f)
      : this.api.adminCreateTopping(f);
    obs.subscribe({
      next: (topping) => {
        if (f.id) {
          this.toppings.update((list) => list.map((t) => (t.id === topping.id ? topping : t)));
        } else {
          this.toppings.update((list) => [...list, topping]);
        }
        this.saving.set(false);
        this.showToppingDialog.set(false);
        this.toast.success('Topping saved');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Failed to save topping');
      },
    });
  }

  deleteTopping(topping: MenuTopping): void {
    if (!confirm(`Delete topping "${topping.name}"?`)) return;
    this.api.adminDeleteTopping(topping.id).subscribe({
      next: () => {
        this.toppings.update((list) => list.filter((t) => t.id !== topping.id));
        this.toast.success('Topping deleted');
      },
      error: () => this.toast.error('Failed to delete topping'),
    });
  }
}
