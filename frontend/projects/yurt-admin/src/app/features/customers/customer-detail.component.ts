import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe } from 'shared-ui';
import { CustomerDetail } from 'shared-models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, Currency2Pipe],
  templateUrl: './customer-detail.component.html',
})
export class CustomerDetailComponent implements OnInit {
  @Input() id!: string;

  private api = inject(YurtApiService);

  customer = signal<CustomerDetail | null>(null);
  loading = signal(true);
  error = signal(false);

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.api.getAdminCustomer(this.id).subscribe({
      next: (c) => { this.customer.set(c); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); },
    });
  }
}
