import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe, ToastService } from 'shared-ui';
import { CustomerDetail } from 'shared-models';
import { environment } from '../../../environments/environment';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './customer-detail.component.html',
})
export class CustomerDetailComponent implements OnInit {
  @Input() id!: string;

  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  customer = signal<CustomerDetail | null>(null);
  loading = signal(true);
  error = signal(false);
  toggleLoading = signal(false);

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.api.getAdminCustomer(this.id).subscribe({
      next: (c) => { this.customer.set(c); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); },
    });
  }

  toggleActive(): void {
    const c = this.customer();
    if (!c) return;
    this.toggleLoading.set(true);
    this.api.setCustomerActive(c.id, !c.isActive).subscribe({
      next: (updated) => {
        this.customer.set(updated);
        this.toggleLoading.set(false);
        this.toast.success(updated.isActive ? 'Customer activated.' : 'Customer deactivated.');
      },
      error: () => {
        this.toggleLoading.set(false);
        this.toast.error('Failed to update customer status.');
      },
    });
  }
}
