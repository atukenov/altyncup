import { Component, inject, signal, effect } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { SkeletonCardComponent, ToastService } from 'shared-ui';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { LocationService } from '../../core/location.service';

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

  select(loc: Location): void {
    this.selectedId.set(loc.id);
    this.locationSvc.setLocation(loc.id, loc.name);
  }

  confirm(): void {
    this.router.navigate(['/menu']);
  }
}