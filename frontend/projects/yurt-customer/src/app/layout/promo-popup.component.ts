import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Promotion } from 'shared-models';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-promo-popup',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './promo-popup.component.html',
  styleUrl: './promo-popup.component.css',
})
export class PromoPopupComponent implements OnInit {
  private api = inject(YurtApiService);

  promotions = signal<Promotion[]>([]);
  currentIndex = signal(0);
  visible = signal(false);

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.api.getActivePromotions().subscribe({
      next: (promos) => {
        if (promos.length > 0) {
          this.promotions.set(promos);
          this.visible.set(true);
        }
      },
    });
  }

  get current(): Promotion | null {
    const promos = this.promotions();
    return promos[this.currentIndex()] ?? null;
  }

  close(): void {
    const nextIndex = this.currentIndex() + 1;
    if (nextIndex < this.promotions().length) {
      this.currentIndex.set(nextIndex);
    } else {
      this.visible.set(false);
    }
  }
}
