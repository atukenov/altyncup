import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuCategory } from 'shared-models';
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

  items = signal<MenuItem[]>([]);
  categories = signal<MenuCategory[]>([]);
  loading = signal(true);
  saving = signal(false);
  showItemDialog = signal(false);
  showCatDialog = signal(false);
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
  }

  categoryName(id: string): string {
    return this.categories().find((c) => c.id === id)?.name ?? '';
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
}