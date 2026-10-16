import { Routes } from '@angular/router';
import { adminAuthGuard, cafeAdminGuard, superAdminGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/admin-login.component').then((m) => m.AdminLoginComponent),
  },
  {
    path: 'auth/change-password',
    loadComponent: () =>
      import('./features/auth/change-password.component').then((m) => m.ChangePasswordComponent),
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
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/menu/menu-management.component').then(
            (m) => m.MenuManagementComponent,
          ),
      },
      {
        path: 'locations',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/locations/locations-management.component').then(
            (m) => m.LocationsManagementComponent,
          ),
      },
      {
        path: 'analytics',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent),
      },
      {
        path: 'promotions',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/promotions/promotions-management.component').then(
            (m) => m.PromotionsManagementComponent,
          ),
      },
      { path: 'dashboard', redirectTo: 'orders', pathMatch: 'full' },
      {
        path: 'customers',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/customers/customers.component').then((m) => m.CustomersComponent),
      },
      {
        path: 'customers/:id',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/customers/customer-detail.component').then((m) => m.CustomerDetailComponent),
      },
      {
        path: 'workers',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/workers/workers.component').then((m) => m.WorkersComponent),
      },
      {
        path: 'audit-log',
        canActivate: [superAdminGuard],
        loadComponent: () =>
          import('./features/audit-log/audit-log.component').then((m) => m.AuditLogComponent),
      },
      {
        path: 'app-releases',
        canActivate: [superAdminGuard],
        loadComponent: () =>
          import('./features/app-releases/app-releases.component').then((m) => m.AppReleasesComponent),
      },
      {
        path: 'discount-codes',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/discount-codes/discount-codes.component').then((m) => m.DiscountCodesComponent),
      },
      {
        path: 'reports',
        canActivate: [cafeAdminGuard],
        loadComponent: () =>
          import('./features/reports/reports.component').then((m) => m.ReportsComponent),
      },
      { path: '', redirectTo: 'orders', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
