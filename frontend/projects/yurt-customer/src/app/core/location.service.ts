import { Injectable, signal, effect, inject } from '@angular/core';
import { YurtApiService } from 'shared-api';
import { LangService } from './lang.service';

@Injectable({ providedIn: 'root' })
export class LocationService {
  private api = inject(YurtApiService);
  private langService = inject(LangService);

  readonly locationId = signal(localStorage.getItem('yurt_location_id') ?? '');
  // Seeded from cache so the name shows instantly on startup; refreshed from API below
  readonly locationName = signal(localStorage.getItem('yurt_location_name') ?? '');

  constructor() {
    effect(() => {
      const id = this.locationId();
      const lang = this.langService.lang();
      if (!id) {
        this.locationName.set('');
        return;
      }
      this.api.getLocations(lang).subscribe({
        next: (locs) => {
          const loc = locs.find((l) => l.id === id);
          if (loc) {
            this.locationName.set(loc.name);
            localStorage.setItem('yurt_location_name', loc.name);
          }
        },
        error: () => {},
      });
    });
  }

  setLocation(id: string, name: string): void {
    localStorage.setItem('yurt_location_id', id);
    localStorage.setItem('yurt_location_name', name);
    this.locationId.set(id);
    this.locationName.set(name); // optimistic; effect re-fetches with fresh DB data
  }
}
