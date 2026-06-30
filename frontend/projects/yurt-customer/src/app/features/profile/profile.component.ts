import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService, NotificationService } from 'shared-api';
import { CustomerProfile, CustomerStats } from 'shared-models';
import { ButtonComponent, ToastService } from 'shared-ui';
import { LangService, Lang } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, TranslatePipe, PullToRefreshDirective],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
})
export class ProfileComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private notifications = inject(NotificationService);
  readonly langService = inject(LangService);
  readonly langs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'ru', label: 'RU' },
    { code: 'kk', label: 'ҚАЗ' },
  ];

  profile = signal<CustomerProfile | null>(null);
  stats = signal<CustomerStats | null>(null);
  editMode = signal(false);
  saving = signal(false);
  editFirstName = '';
  editLastName = '';

  // Change PIN
  showPinForm = signal(false);
  currentPin = '';
  newPin = '';
  pinLoading = signal(false);

  // Delete account
  showDeleteConfirm = signal(false);
  deleteLoading = signal(false);

  // Notifications
  notificationsEnabled = signal(localStorage.getItem('yurt_push_enabled') !== 'false');

  // Report a Problem
  showReportModal = signal(false);
  reportText = '';
  reportLoading = signal(false);

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.api.me().subscribe({ next: (p) => this.profile.set(p), error: () => {} });
    this.api.getCustomerStats().subscribe({ next: (s) => this.stats.set(s), error: () => {} });
  }

  async toggleNotifications(): Promise<void> {
    const current = this.notificationsEnabled();
    if (!current) {
      await this.notifications.requestWebNotificationPermission();
    }
    this.notificationsEnabled.set(!current);
    localStorage.setItem('yurt_push_enabled', (!current).toString());
  }

  submitPinChange(): void {
    if (!this.currentPin || !this.newPin || this.currentPin.length !== 4 || this.newPin.length !== 4) {
      this.toast.error('Both PINs must be exactly 4 digits.');
      return;
    }
    this.pinLoading.set(true);
    this.api.changePin(this.currentPin, this.newPin).subscribe({
      next: () => {
        this.showPinForm.set(false);
        this.currentPin = '';
        this.newPin = '';
        this.pinLoading.set(false);
        this.toast.success('PIN changed successfully.');
      },
      error: (err) => {
        this.pinLoading.set(false);
        this.toast.error(err.status === 401 ? 'Current PIN is incorrect.' : 'Failed to change PIN.');
      },
    });
  }

  confirmDelete(): void {
    this.deleteLoading.set(true);
    this.api.deleteAccount().subscribe({
      next: () => {
        this.auth.logout();
        this.toast.info('Account deleted.');
        this.router.navigate(['/auth/login']);
      },
      error: () => {
        this.deleteLoading.set(false);
        this.toast.error('Failed to delete account.');
      },
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

  submitReport(): void {
    const text = this.reportText.trim();
    if (!text) return;
    this.reportLoading.set(true);
    this.api.submitReport(text).subscribe({
      next: () => {
        this.reportLoading.set(false);
        this.reportText = '';
        this.showReportModal.set(false);
        this.toast.success(this.langService.t('profile.reportSuccess'));
      },
      error: () => {
        this.reportLoading.set(false);
        this.toast.error('Failed to send report. Please try again.');
      },
    });
  }
}
