import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { SkeletonCardComponent, ToastService } from 'shared-ui';

const SELECTED_LOCATION_KEY = 'yurt_location_id';

@Component({
  selector: 'app-locations',
  standalone: true,
  imports: [CommonModule, SkeletonCardComponent],
  templateUrl: './locations.component.html',
  styleUrl: './locations.component.css',
})
export class LocationsComponent implements OnInit {
  private api = inject(YurtApiService);
  private router = inject(Router);
  private toast = inject(ToastService);

  locations = signal<Location[]>([]);
  loading = signal(true);
  selectedId = signal<string | null>(localStorage.getItem(SELECTED_LOCATION_KEY));

  ngOnInit(): void {
    this.api.getLocations().subscribe({
      next: (locs) => {
        this.locations.set(locs);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load locations.');
      },
    });
  }

  select(loc: Location): void {
    this.selectedId.set(loc.id);
    localStorage.setItem(SELECTED_LOCATION_KEY, loc.id);
    localStorage.setItem('yurt_location_name', loc.name);
  }

  confirm(): void {
    this.router.navigate(['/menu']);
  }
}