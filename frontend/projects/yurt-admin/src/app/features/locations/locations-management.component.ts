import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { ButtonComponent, ToastService } from 'shared-ui';

interface LocationForm {
  id?: string;
  name: string;
  address: string;
  workingHours: string;
  contactPhone: string;
  isActive: boolean;
}

@Component({
  selector: 'app-locations-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './locations-management.component.html',
  styleUrl: './locations-management.component.css',
})
export class LocationsManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  locations = signal<Location[]>([]);
  loading = signal(true);
  saving = signal(false);
  showDialog = signal(false);
  form = signal<LocationForm>({
    name: '',
    address: '',
    workingHours: '',
    contactPhone: '',
    isActive: true,
  });

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({
      next: (locs) => {
        this.locations.set(locs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openDialog(loc?: Location): void {
    this.form.set({
      id: loc?.id,
      name: loc?.name ?? '',
      address: loc?.address ?? '',
      workingHours: loc?.workingHours ?? '',
      contactPhone: loc?.contactPhone ?? '',
      isActive: loc?.isActive ?? true,
    });
    this.showDialog.set(true);
  }

  patch(field: keyof LocationForm, value: unknown): void {
    this.form.update((f) => ({ ...f, [field]: value }));
  }

  save(): void {
    const f = this.form();
    if (!f.name || !f.address) {
      this.toast.error('Name and address are required');
      return;
    }
    this.saving.set(true);
    const obs = f.id ? this.api.updateLocation(f.id, f) : this.api.createLocation(f);
    obs.subscribe({
      next: (loc) => {
        if (f.id) {
          this.locations.update((list) => list.map((l) => (l.id === loc.id ? loc : l)));
        } else {
          this.locations.update((list) => [...list, loc]);
        }
        this.saving.set(false);
        this.showDialog.set(false);
        this.toast.success('Location saved');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Failed to save location');
      },
    });
  }

  deleteLocation(loc: Location): void {
    if (!confirm(`Delete "${loc.name}"?`)) return;
    this.api.deleteLocation(loc.id).subscribe({
      next: () => {
        this.locations.update((list) => list.filter((l) => l.id !== loc.id));
        this.toast.success('Location deleted');
      },
      error: () => this.toast.error('Failed to delete location'),
    });
  }
}