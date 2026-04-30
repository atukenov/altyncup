import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { MenuItem } from 'shared-models';
import { ToastService, Currency2Pipe, SkeletonCardComponent } from 'shared-ui';
import { CartService } from '../cart/cart.service';

@Component({
  selector: 'app-favorites',
  standalone: true,
  imports: [CommonModule, RouterLink, Currency2Pipe, SkeletonCardComponent],
  templateUrl: './favorites.component.html',
  styleUrl: './favorites.component.css',
})
export class FavoritesComponent implements OnInit {
  private api = inject(YurtApiService);
  private cart = inject(CartService);
  private toast = inject(ToastService);

  loading = signal(true);
  items = signal<MenuItem[]>([]);

  ngOnInit(): void {
    this.api.getFavorites().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load favorites.');
      },
    });
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
}
