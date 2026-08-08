import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { CustomerStats } from 'shared-models';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { ACHIEVEMENTS, Achievement } from '../news/achievements';

@Component({
  selector: 'app-achievements',
  standalone: true,
  imports: [RouterLink, TranslatePipe],
  templateUrl: './achievements.component.html',
  styleUrl: './achievements.component.css',
})
export class AchievementsComponent implements OnInit {
  private api = inject(YurtApiService);
  readonly lang = inject(LangService);

  readonly loading = signal(true);
  readonly stats = signal<CustomerStats>({ totalOrders: 0, totalSpent: 0 });
  readonly animatedCount = signal(0);

  readonly total = ACHIEVEMENTS.length;
  readonly circumference = 2 * Math.PI * 40;

  readonly unlockedIds = computed(() => {
    const s = this.stats();
    const flags = { wolt: localStorage.getItem('yurt_wolt_clicked') };
    return new Set(ACHIEVEMENTS.filter((a) => a.condition(s, flags)).map((a) => a.id));
  });

  readonly sortedAchievements = computed(() => {
    const ids = this.unlockedIds();
    return [...ACHIEVEMENTS].sort((a, b) => {
      const au = ids.has(a.id) ? 0 : 1;
      const bu = ids.has(b.id) ? 0 : 1;
      return au - bu;
    });
  });

  readonly ringOffset = computed(() =>
    this.circumference * (1 - this.animatedCount() / this.total)
  );

  ngOnInit(): void {
    this.api.getCustomerStats().subscribe({
      next: (stats) => {
        this.stats.set(stats);
        this.loading.set(false);
        const unlocked = this.unlockedIds().size;
        setTimeout(() => this.animateCount(0, unlocked, 700), 250);
      },
      error: () => this.loading.set(false),
    });
  }

  private animateCount(from: number, to: number, duration: number): void {
    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - t, 3);
      this.animatedCount.set(Math.round(from + (to - from) * eased));
      if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  isUnlocked(a: Achievement): boolean {
    return this.unlockedIds().has(a.id);
  }

  name(a: Achievement): string {
    const l = this.lang.lang();
    return l === 'kk' ? a.nameKk : l === 'ru' ? a.nameRu : a.nameEn;
  }

  desc(a: Achievement): string {
    const l = this.lang.lang();
    return l === 'kk' ? a.descKk : l === 'ru' ? a.descRu : a.descEn;
  }

  progressInfo(a: Achievement): { pct: number; label: string } | null {
    const s = this.stats();
    const l = this.lang.lang();
    const ordLabel = l === 'ru' ? 'заказов' : l === 'kk' ? 'тапсырыс' : 'orders';
    type Entry = { current: number; target: number; type: 'orders' | 'spent' };
    const map: Record<string, Entry> = {
      first_sip:      { current: s.totalOrders, target: 1,     type: 'orders' },
      coffee_rookie:  { current: s.totalOrders, target: 3,     type: 'orders' },
      golden_cup:     { current: s.totalOrders, target: 10,    type: 'orders' },
      altyn_regular:  { current: s.totalOrders, target: 25,    type: 'orders' },
      altyncup_star:  { current: s.totalOrders, target: 50,    type: 'orders' },
      century_sipper: { current: s.totalOrders, target: 100,   type: 'orders' },
      big_spender:    { current: s.totalSpent,  target: 10000, type: 'spent'  },
      altyn_champion: { current: s.totalSpent,  target: 50000, type: 'spent'  },
    };
    const p = map[a.id];
    if (!p) return null;
    const pct = Math.min(1, p.current / p.target);
    const label = p.type === 'spent'
      ? `${Math.round(p.current / 1000)}K / ${p.target / 1000}K ₸`
      : `${p.current} / ${p.target} ${ordLabel}`;
    return { pct, label };
  }
}
