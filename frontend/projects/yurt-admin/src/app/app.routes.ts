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
      { path: '', redirectTo: 'orders', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
