import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService } from 'shared-api';
import { CustomerProfile } from 'shared-models';
import { ButtonComponent, ToastService } from 'shared-ui';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
})
export class ProfileComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);

  profile = signal<CustomerProfile | null>(null);
  editMode = signal(false);
  saving = signal(false);
  editFirstName = '';
  editLastName = '';

  ngOnInit(): void {
    this.api.me().subscribe({
      next: (p) => this.profile.set(p),
      error: () => {},
    });
  }

  startEdit(): void {
    const p = this.profile();
    this.editFirstName = p?.firstName ?? '';
    this.editLastName = p?.lastName ?? '';
    this.editMode.set(true);
  }

  saveProfile(): void {
    if (!this.editFirstName.trim() || !this.editLastName.trim()) {
      this.toast.error('First and last name are required.');
      return;
    }
    this.saving.set(true);
    this.api
      .updateProfile({ firstName: this.editFirstName.trim(), lastName: this.editLastName.trim() })
      .subscribe({
        next: (p) => {
          this.profile.set(p);
          this.editMode.set(false);
          this.saving.set(false);
          this.toast.success('Profile updated.');
        },
        error: () => {
          this.saving.set(false);
          this.toast.error('Failed to update profile.');
        },
      });
  }

  logout(): void {
    this.auth.logout();
    this.toast.info('Signed out');
    this.router.navigate(['/auth/login']);
  }
}
