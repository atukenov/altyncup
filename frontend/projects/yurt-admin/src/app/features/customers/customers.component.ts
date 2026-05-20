import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe } from 'shared-ui';
import { CustomerSummary } from 'shared-models';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, Currency2Pipe],
  templateUrl: './customers.component.html',
})
export class CustomersComponent implements OnInit {
  private api = inject(YurtApiService);

  customers = signal<CustomerSummary[]>([]);
  loading = signal(true);
  phone = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getAdminCustomers(this.phone || undefined).subscribe({
      next: (c) => { this.customers.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  onSearch(): void {
    this.load();
  }
}
