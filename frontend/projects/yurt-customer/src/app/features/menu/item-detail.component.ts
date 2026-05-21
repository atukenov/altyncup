import { Component, inject, OnInit, signal, Input, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { MenuItem } from 'shared-models';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-item-detail',
  standalone: true,
  imports: [CommonModule, ButtonComponent, Currency2Pipe, TranslatePipe],
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
    this.loadFavorites();
  }

  loadFavorites(): void {
    this.api.getFavorites().subscribe({
      next: (favs) => this.isFav.set(favs.some((f) => f.id === this.id)),
      error: () => {},
    });
  }

  incrementQty(): void {
    this.quantity.update((q) => q + 1);
  }
  decrementQty(): void {
    if (this.quantity() > 1) this.quantity.update((q) => q - 1);
  }

  addToCart(): void {
    const item = this.item();
    if (!item) return;
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: item.price,
      quantity: this.quantity(),
      imageUrl: item.imageUrl,
    });
    this.toast.success(`${this.quantity()}x ${item.name} added to cart`);
    this.router.navigate(['/menu']);
  }

  toggleFavorite(): void {
    const id = this.id;
    if (this.isFav()) {
      this.api.removeFavorite(id).subscribe(() => {
        this.isFav.set(false);
        this.toast.info('Removed from favorites');
      });
    } else {
      this.api.addFavorite(id).subscribe(() => {
        this.isFav.set(true);
        this.toast.success('Added to favorites ♥');
      });
    }
  }

  goBack(): void {
    this.router.navigate(['/menu']);
  }
}