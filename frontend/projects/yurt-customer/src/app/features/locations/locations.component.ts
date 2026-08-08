import { Component, inject, signal, effect } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { SkeletonCardComponent, ToastService } from 'shared-ui';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { LocationService } from '../../core/location.service';

interface WorkingSlot {
  day: number; // 1=Mon … 7=Sun
  enabled: boolean;
  from: string; // 'HH:MM'
  to: string;
}

export interface ScheduleInfo {
  isOpen: boolean;
  todayLabel: string;     // "08:00 – 22:00" or "Closed today"
  nextOpenAt: string | null; // "08:00" when closed and opens later/tomorrow
  weekSummary: string[];  // ["Mon–Fri  08:00–22:00", "Sat–Sun  Closed"]
}

const DAY_ABBR = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

function parseSlots(wh: string): WorkingSlot[] | null {
  try {
    const p = JSON.parse(wh);
    if (Array.isArray(p) && p.length === 7) return p as WorkingSlot[];
  } catch { /* legacy plain-text, ignore */ }
  return null;
}

function toMin(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number);
  return (h || 0) * 60 + (m || 0);
}

function buildScheduleInfo(wh: string, now: Date): ScheduleInfo {
  const slots = parseSlots(wh);

  if (!slots) {
    // Legacy plain-text — can't compute status, just show raw value
    return {
      isOpen: true,
      todayLabel: wh || '—',
      nextOpenAt: null,
      weekSummary: [wh || '—'],
    };
  }

  // Current day  (1=Mon … 7=Sun)
  const jsDay = now.getDay();
  const today = jsDay === 0 ? 7 : jsDay;
  const nowMin = now.getHours() * 60 + now.getMinutes();

  // Today's slot
  const todaySlot = slots.find((s) => s.day === today)!;
  const todayOpen = todaySlot.enabled && nowMin >= toMin(todaySlot.from) && nowMin < toMin(todaySlot.to);
  const todayLabel = todaySlot.enabled
    ? `${todaySlot.from} – ${todaySlot.to}`
    : 'Closed today';

  // Next open time (when closed)
  let nextOpenAt: string | null = null;
  if (!todayOpen) {
    // Check today after current time
    if (todaySlot.enabled && nowMin < toMin(todaySlot.from)) {
      nextOpenAt = todaySlot.from;
    } else {
      // Search upcoming days
      for (let i = 1; i <= 7; i++) {
        const nextDay = ((today - 1 + i) % 7) + 1;
        const s = slots.find((sl) => sl.day === nextDay && sl.enabled);
        if (s) { nextOpenAt = s.from; break; }
      }
    }
  }

  // Week summary — group consecutive slots with identical enabled + hours
  interface Group { days: number[]; enabled: boolean; from: string; to: string; }
  const groups: Group[] = [];
  for (const slot of slots) {
    const last = groups[groups.length - 1];
    if (last && last.enabled === slot.enabled && last.from === slot.from && last.to === slot.to) {
      last.days.push(slot.day);
    } else {
      groups.push({ days: [slot.day], enabled: slot.enabled, from: slot.from, to: slot.to });
    }
  }

  const weekSummary = groups.map((g) => {
    const first = DAY_ABBR[g.days[0] - 1];
    const last  = DAY_ABBR[g.days[g.days.length - 1] - 1];
    const dayStr = g.days.length === 1 ? first : `${first}–${last}`;
    return g.enabled ? `${dayStr}  ${g.from}–${g.to}` : `${dayStr}  Closed`;
  });

  return { isOpen: todayOpen, todayLabel, nextOpenAt, weekSummary };
}

@Component({
  selector: 'app-locations',
  standalone: true,
  imports: [CommonModule, SkeletonCardComponent, TranslatePipe],
  templateUrl: './locations.component.html',
  styleUrl: './locations.component.css',
})
export class LocationsComponent {
  private api = inject(YurtApiService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private langService = inject(LangService);
  private locationSvc = inject(LocationService);

  locations = signal<Location[]>([]);
  loading = signal(true);
  selectedId = signal<string | null>(this.locationSvc.locationId() || null);

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      this.loading.set(true);
      this.api.getLocations(lang).subscribe({
        next: (locs) => { this.locations.set(locs); this.loading.set(false); },
        error: () => { this.loading.set(false); this.toast.error('Failed to load locations.'); },
      });
    });
  }

  scheduleOf(loc: Location): ScheduleInfo {
    return buildScheduleInfo(loc.workingHours, new Date());
  }

  select(loc: Location): void {
    this.selectedId.set(loc.id);
    this.locationSvc.setLocation(loc.id, loc.name);
  }

  confirm(): void {
    this.router.navigate(['/menu']);
  }
}
