import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuCategory, MenuTopping } from 'shared-models';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

interface MenuItemForm {
  id?: string;
  name: string;
  nameRu: string;
  nameKk: string;
  description: string;
  descriptionRu: string;
  descriptionKk: string;
  price: number;
  categoryId: string;
  imageUrl: string;
  isAvailable: boolean;
}

interface ToppingForm {
  id?: string;
  name: string;
  nameRu: string;
  nameKk: string;
  price: number;
  isAvailable: boolean;
  categoryIds: string[];
  group: string;
}

interface CatInput {
  id?: string;
  name: string;
  nameRu: string;
  nameKk: string;
}

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.css',
})
export class MenuManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);
  readonly langService = inject(AdminLangService);

  activeTab = signal<'items' | 'toppings'>('items');
  mobileCatOpen = signal(false);

  items = signal<MenuItem[]>([]);
  categories = signal<MenuCategory[]>([]);
  toppings = signal<MenuTopping[]>([]);
  loading = signal(true);
  saving = signal(false);
  showItemDialog = signal(false);
  showCatDialog = signal(false);
  showToppingDialog = signal(false);
  selectedCategoryId = signal('');
  catInput = signal<CatInput>({ name: '', nameRu: '', nameKk: '' });

  itemForm = signal<MenuItemForm>({
    name: '', nameRu: '', nameKk: '',
    description: '', descriptionRu: '', descriptionKk: '',
    price: 0, categoryId: '', imageUrl: '', isAvailable: true,
  });

  toppingForm = signal<ToppingForm>({
    name: '', nameRu: '', nameKk: '',
    price: 0, isAvailable: true, categoryIds: [], group: '',
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
      next: (items) => { this.items.set(items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.adminGetToppings().subscribe((toppings) => this.toppings.set(toppings));
  }

  localizedCatName(cat: MenuCategory): string {
    const lang = this.langService.lang();
    if (lang === 'ru') return cat.nameRu || cat.name;
    if (lang === 'kk') return cat.nameKk || cat.nameRu || cat.name;
    return cat.name;
  }

  categoryName(id: string): string {
    const cat = this.categories().find((c) => c.id === id);
    return cat ? this.localizedCatName(cat) : '';
  }

  toppingCategoryNames(categoryIds: string[]): string {
    return categoryIds.map((id) => this.categoryName(id)).filter(Boolean).join(', ') || '—';
  }

  // Active-language field getters/setters for item form
  getActiveItemName(): string {
    const f = this.itemForm();
    const lang = this.langService.lang();
    if (lang === 'ru') return f.nameRu;
    if (lang === 'kk') return f.nameKk;
    return f.name;
  }
  setActiveItemName(val: string): void {
    const lang = this.langService.lang();
    if (lang === 'ru') this.patchItemForm('nameRu', val);
    else if (lang === 'kk') this.patchItemForm('nameKk', val);
    else this.patchItemForm('name', val);
  }

  getActiveItemDescription(): string {
    const f = this.itemForm();
    const lang = this.langService.lang();
    if (lang === 'ru') return f.descriptionRu;
    if (lang === 'kk') return f.descriptionKk;
    return f.description;
  }
  setActiveItemDescription(val: string): void {
    const lang = this.langService.lang();
    if (lang === 'ru') this.patchItemForm('descriptionRu', val);
    else if (lang === 'kk') this.patchItemForm('descriptionKk', val);
    else this.patchItemForm('description', val);
  }

  // Active-language field getters/setters for topping form
  getActiveToppingName(): string {
    const f = this.toppingForm();
    const lang = this.langService.lang();
    if (lang === 'ru') return f.nameRu;
    if (lang === 'kk') return f.nameKk;
    return f.name;
  }
  setActiveToppingName(val: string): void {
    const lang = this.langService.lang();
    if (lang === 'ru') this.patchToppingForm('nameRu', val);
    else if (lang === 'kk') this.patchToppingForm('nameKk', val);
    else this.patchToppingForm('name', val);
  }

  // Active-language field getters/setters for category input
  getActiveCatName(): string {
    const f = this.catInput();
    const lang = this.langService.lang();
    if (lang === 'ru') return f.nameRu;
    if (lang === 'kk') return f.nameKk;
    return f.name;
  }
  setActiveCatName(val: string): void {
    const lang = this.langService.lang();
    if (lang === 'ru') this.catInput.update((f) => ({ ...f, nameRu: val }));
    else if (lang === 'kk') this.catInput.update((f) => ({ ...f, nameKk: val }));
    else this.catInput.update((f) => ({ ...f, name: val }));
  }

  langLabel(): string {
    return this.langService.lang().toUpperCase();
  }

  openItemDialog(item?: MenuItem): void {
    this.itemForm.set({
      id: item?.id,
      name: item?.name ?? '',
      nameRu: item?.nameRu ?? '',
      nameKk: item?.nameKk ?? '',
      description: item?.description ?? '',
      descriptionRu: item?.descriptionRu ?? '',
      descriptionKk: item?.descriptionKk ?? '',
      price: item?.price ?? 0,
      categoryId: item?.categoryId ?? (this.selectedCategoryId() || this.categories()[0]?.id) ?? '',
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
    if (!f.name && !f.nameRu) {
      this.toast.error('Name is required');
      return;
    }
    if (!f.price) {
      this.toast.error('Price is required');
      return;
    }
    this.saving.set(true);
    const payload = {
      name: f.name || f.nameRu,
      nameRu: f.nameRu || undefined,
      nameKk: f.nameKk || undefined,
      description: f.description || f.descriptionRu,
      descriptionRu: f.descriptionRu || undefined,
      descriptionKk: f.descriptionKk || undefined,
      price: f.price,
      categoryId: f.categoryId,
      imageUrl: f.imageUrl || undefined,
      isAvailable: f.isAvailable,
    };
    const obs = f.id ? this.api.adminUpdateMenuItem(f.id, payload) : this.api.adminCreateMenuItem(payload);
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
      error: () => { this.saving.set(false); this.toast.error('Failed to save item'); },
    });
  }

  async deleteItem(item: MenuItem): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Item', `Delete "${item.name}"?`)) return;
    this.api.adminDeleteMenuItem(item.id).subscribe({
      next: () => {
        this.items.update((list) => list.filter((i) => i.id !== item.id));
        this.toast.success('Item deleted');
      },
      error: () => this.toast.error('Failed to delete item'),
    });
  }

  saveCat(): void {
    const f = this.catInput();
    const name = f.name.trim() || f.nameRu.trim();
    if (!name) return;
    this.saving.set(true);
    const payload = {
      name,
      nameRu: f.nameRu || undefined,
      nameKk: f.nameKk || undefined,
    };
    const obs = f.id
      ? this.api.adminUpdateCategory(f.id, payload)
      : this.api.adminCreateCategory(payload);
    obs.subscribe({
      next: (cat) => {
        if (f.id) {
          this.categories.update((list) => list.map((c) => (c.id === cat.id ? cat : c)));
        } else {
          this.categories.update((list) => [...list, cat]);
        }
        this.saving.set(false);
        this.showCatDialog.set(false);
        this.toast.success(f.id ? 'Category updated' : 'Category created');
      },
      error: () => { this.saving.set(false); this.toast.error('Failed to save category'); },
    });
  }

  async deleteCat(cat: MenuCategory): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Category', `Delete category "${this.localizedCatName(cat)}"?`)) return;
    this.api.adminDeleteCategory(cat.id).subscribe({
      next: () => {
        this.categories.update((list) => list.filter((c) => c.id !== cat.id));
        if (this.selectedCategoryId() === cat.id) this.selectedCategoryId.set('');
        this.toast.success('Category deleted');
      },
      error: () => this.toast.error('Failed to delete category'),
    });
  }

  openCatDialog(cat?: MenuCategory): void {
    this.catInput.set({
      id: cat?.id,
      name: cat?.name ?? '',
      nameRu: cat?.nameRu ?? '',
      nameKk: cat?.nameKk ?? '',
    });
    this.showCatDialog.set(true);
  }

  openToppingDialog(topping?: MenuTopping): void {
    this.toppingForm.set({
      id: topping?.id,
      name: topping?.name ?? '',
      nameRu: topping?.nameRu ?? '',
      nameKk: topping?.nameKk ?? '',
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
    const name = f.name || f.nameRu;
    if (!name) { this.toast.error('Topping name is required'); return; }
    this.saving.set(true);
    const payload = {
      name,
      nameRu: f.nameRu || undefined,
      nameKk: f.nameKk || undefined,
      price: f.price,
      isAvailable: f.isAvailable,
      categoryIds: f.categoryIds,
      group: f.group || undefined,
    };
    const obs = f.id ? this.api.adminUpdateTopping(f.id, payload) : this.api.adminCreateTopping(payload);
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
      error: () => { this.saving.set(false); this.toast.error('Failed to save topping'); },
    });
  }

  async deleteTopping(topping: MenuTopping): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Topping', `Delete topping "${topping.name}"?`)) return;
    this.api.adminDeleteTopping(topping.id).subscribe({
      next: () => {
        this.toppings.update((list) => list.filter((t) => t.id !== topping.id));
        this.toast.success('Topping deleted');
      },
      error: () => this.toast.error('Failed to delete topping'),
    });
  }
}
