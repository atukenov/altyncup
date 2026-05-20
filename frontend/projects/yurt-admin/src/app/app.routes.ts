import { Routes } from '@angular/router';
import { adminAuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/admin-login.component').then((m) => m.AdminLoginComponent),
  },
  {
    path: '',
    canActivateChild: [adminAuthGuard],
    loadComponent: () =>
      import('./layout/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      {
        path: 'orders',
        loadComponent: () =>
          import('./features/orders/orders-live.component').then((m) => m.OrdersLiveComponent),
      },
      {
        path: 'menu',
        loadComponent: () =>
          import('./features/menu/menu-management.component').then(
            (m) => m.MenuManagementComponent,
          ),
      },
      {
        path: 'locations',
        loadComponent: () =>
          import('./features/locations/locations-management.component').then(
            (m) => m.LocationsManagementComponent,
          ),
      },
      {
        path: 'analytics',
        loadComponent: () =>
          import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent),
      },
      {
        path: 'promotions',
        loadComponent: () =>
          import('./features/promotions/promotions-management.component').then(
            (m) => m.PromotionsManagementComponent,
          ),
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'customers',
        loadComponent: () =>
          import('./features/customers/customers.component').then((m) => m.CustomersComponent),
      },
      {
        path: 'customers/:id',
        loadComponent: () =>
          import('./features/customers/customer-detail.component').then((m) => m.CustomerDetailComponent),
      },
      {
        path: 'workers',
        loadComponent: () =>
          import('./features/workers/workers.component').then((m) => m.WorkersComponent),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
