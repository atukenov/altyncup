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

function parseSlots(wh: string): WorkingSlot[] | null {
  try {
    const p = JSON.parse(wh);
    if (Array.isArray(p) && p.length === 7) return p;
  } catch { /* legacy plain-text */ }
  return null;
}

function toMinutes(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
}

function isOpen(slots: WorkingSlot[], now: Date): boolean {
  const jsDay = now.getDay(); // 0=Sun
  const day = jsDay === 0 ? 7 : jsDay; // 1=Mon…7=Sun
  const slot = slots.find((s) => s.day === day);
  if (!slot?.enabled) return false;
  const nowMin = now.getHours() * 60 + now.getMinutes();
  return nowMin >= toMinutes(slot.from) && nowMin < toMinutes(slot.to);
}

function nextOpenTime(slots: WorkingSlot[], now: Date): string | null {
  const jsDay = now.getDay();
  const day = jsDay === 0 ? 7 : jsDay;
  const nowMin = now.getHours() * 60 + now.getMinutes();

  // Still opens today (haven't reached opening time yet)
  const todaySlot = slots.find((s) => s.day === day && s.enabled);
  if (todaySlot && nowMin < toMinutes(todaySlot.from)) return todaySlot.from;

  // Next open day within the week
  for (let i = 1; i <= 7; i++) {
    const nextDay = ((day - 1 + i) % 7) + 1;
    const slot = slots.find((s) => s.day === nextDay && s.enabled);
    if (slot) return slot.from;
  }
  return null;
}

function formatHoursDisplay(wh: string): string {
  const slots = parseSlots(wh);
  if (!slots) return wh;
  const open = slots.filter((s) => s.enabled);
  if (!open.length) return '—';
  const allSame = open.every((s) => s.from === open[0].from && s.to === open[0].to);
  const timeStr = `${open[0].from}–${open[0].to}`;
  if (open.length === 7) return allSame ? `Daily ${timeStr}` : 'Daily';
  const dayAbbr = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const names = open.map((s) => dayAbbr[s.day - 1]);
  return allSame ? `${names.join(', ')} ${timeStr}` : `${open.length} days`;
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

  private now = new Date();

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

  isLocationOpen(loc: Location): boolean {
    const slots = parseSlots(loc.workingHours);
    if (!slots) return true; // legacy format — assume open
    return isOpen(slots, this.now);
  }

  nextOpenStr(loc: Location): string | null {
    const slots = parseSlots(loc.workingHours);
    if (!slots) return null;
    return nextOpenTime(slots, this.now);
  }

  formatHours(loc: Location): string {
    return formatHoursDisplay(loc.workingHours);
  }

  select(loc: Location): void {
    this.selectedId.set(loc.id);
    this.locationSvc.setLocation(loc.id, loc.name);
  }

  confirm(): void {
    this.router.navigate(['/menu']);
  }
}
