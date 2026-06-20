import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LocationService {
  readonly locationId = signal(localStorage.getItem('yurt_location_id') ?? '');
  readonly locationName = signal(localStorage.getItem('yurt_location_name') ?? '');

  setLocation(id: string, name: string): void {
    localStorage.setItem('yurt_location_id', id);
    localStorage.setItem('yurt_location_name', name);
    this.locationId.set(id);
    this.locationName.set(name);
  }
}
