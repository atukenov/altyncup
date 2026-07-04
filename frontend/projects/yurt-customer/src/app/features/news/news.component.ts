import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { YurtApiService } from 'shared-api';
import { CustomerStats, MenuItem, Order, Promotion } from 'shared-models';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Currency2Pipe } from 'shared-ui';
import { DatePipe } from '@angular/common';
import { ACHIEVEMENTS, Achievement } from './achievements';

@Component({
  selector: 'app-news',
  standalone: true,
  imports: [RouterLink, TranslatePipe, Currency2Pipe, DatePipe],
  templateUrl: './news.component.html',
  styleUrl: './news.component.css',
})
export class NewsComponent implements OnInit {
  private api = inject(YurtApiService);
  private cart = inject(CartService);
  private router = inject(Router);
  readonly lang = inject(LangService);

  readonly loading = signal(true);
  readonly stats = signal<CustomerStats>({ totalOrders: 0, totalSpent: 0 });
  readonly promotions = signal<Promotion[]>([]);
  readonly menuItems = signal<MenuItem[]>([]);
  readonly orderHistory = signal<Order[]>([]);

  readonly animatedOrders = signal(0);
  readonly animatedBonuses = signal(0);
  readonly animatedAchievements = signal(0);

  readonly newItems = computed(() =>
    [...this.menuItems()]
      .filter((i) => i.createdAt)
      .sort((a, b) => new Date(b.createdAt!).getTime() - new Date(a.createdAt!).getTime())
      .slice(0, 6)
  );

  readonly recommendedItems = computed(() => {
    const orders = this.orderHistory();
    const items = this.menuItems();
    if (!orders.length) return items.slice(0, 6);

    const orderedIds = new Set(orders.flatMap((o) => o.items.map((i) => i.menuItemId)));
    const categoryFreq: Record<string, number> = {};
    orders.forEach((o) =>
      o.items.forEach((i) => {
        const item = items.find((m) => m.id === i.menuItemId);
        if (item) categoryFreq[item.categoryId] = (categoryFreq[item.categoryId] ?? 0) + 1;
      })
    );

    const sorted = items
      .filter((i) => !orderedIds.has(i.id))
      .sort((a, b) => (categoryFreq[b.categoryId] ?? 0) - (categoryFreq[a.categoryId] ?? 0));

    return sorted.length >= 6 ? sorted.slice(0, 6) : items.slice(0, 6);
  });

  readonly recentOrders = computed(() =>
    this.orderHistory()
      .filter((o) => o.status === 'Completed')
      .slice(0, 2)
  );

  readonly unlockedAchievements = computed(() => {
    const s = this.stats();
    const flags = { wolt: localStorage.getItem('yurt_wolt_clicked') };
    return ACHIEVEMENTS.filter((a) => a.condition(s, flags));
  });

  readonly allAchievements: Achievement[] = ACHIEVEMENTS;

  ngOnInit(): void {
    forkJoin({
      stats: this.api.getCustomerStats().pipe(catchError(() => of<CustomerStats>({ totalOrders: 0, totalSpent: 0 }))),
      promotions: this.api.getActivePromotions().pipe(catchError(() => of<Promotion[]>([]))),
      items: this.api.getMenuItems(undefined, undefined, this.lang.lang()).pipe(catchError(() => of<MenuItem[]>([]))),
      orders: this.api.getOrderHistory().pipe(catchError(() => of<Order[]>([]))),
    }).subscribe(({ stats, promotions, items, orders }) => {
      this.stats.set(stats);
      this.promotions.set(promotions);
      this.menuItems.set(items);
      this.orderHistory.set(orders);
      this.loading.set(false);

      const unlocked = ACHIEVEMENTS.filter((a) =>
        a.condition(stats, { wolt: localStorage.getItem('yurt_wolt_clicked') })
      ).length;

      setTimeout(() => this.animateCount(0, stats.totalOrders, 800, this.animatedOrders.set.bind(this.animatedOrders)), 100);
      setTimeout(() => this.animateCount(0, 0, 800, this.animatedBonuses.set.bind(this.animatedBonuses)), 200);
      setTimeout(() => this.animateCount(0, unlocked, 600, this.animatedAchievements.set.bind(this.animatedAchievements)), 300);
    });
  }

  private animateCount(from: number, to: number, duration: number, setter: (v: number) => void): void {
    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - t, 3);
      setter(Math.round(from + (to - from) * eased));
      if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  achievementName(a: Achievement): string {
    const l = this.lang.lang();
    if (l === 'kk') return a.nameKk;
    if (l === 'ru') return a.nameRu;
    return a.nameEn;
  }

  openWolt(): void {
    localStorage.setItem('yurt_wolt_clicked', 'true');
    window.open('https://wolt.com/en/kaz', '_blank');
  }

  reorder(order: Order): void {
    order.items.forEach((item) => {
      this.cart.addItem({
        menuItemId: item.menuItemId,
        name: item.menuItemName,
        price: item.unitPrice,
        quantity: item.quantity,
        selectedToppings: item.toppings.map((t) => ({
          toppingId: t.toppingId,
          toppingName: t.toppingName,
          price: t.price,
        })),
        notes: item.notes,
        variantLabel: item.variantLabel,
      });
    });
    this.router.navigate(['/cart']);
  }

  addToCart(item: MenuItem): void {
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      nameRu: item.nameRu,
      nameKk: item.nameKk,
      price: item.price,
      quantity: 1,
      imageUrl: item.imageUrl,
    });
  }

  get totalAchievements(): number {
    return ACHIEVEMENTS.length;
  }
}
