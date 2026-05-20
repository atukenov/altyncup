import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'auth/login',
    pathMatch: 'full',
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    canActivateChild: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      {
        path: 'locations',
        loadComponent: () =>
          import('./features/locations/locations.component').then((m) => m.LocationsComponent),
      },
      {
        path: 'menu',
        loadChildren: () => import('./features/menu/menu.routes').then((m) => m.MENU_ROUTES),
      },
      {
        path: 'cart',
        loadComponent: () => import('./features/cart/cart.component').then((m) => m.CartComponent),
      },
      {
        path: 'orders',
        loadChildren: () => import('./features/orders/orders.routes').then((m) => m.ORDER_ROUTES),
      },
      {
        path: 'favorites',
        loadComponent: () =>
          import('./features/favorites/favorites.component').then((m) => m.FavoritesComponent),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/profile/profile.component').then((m) => m.ProfileComponent),
      },
      {
        path: 'group-order/create',
        loadComponent: () =>
          import('./features/group-order/group-order-create.component').then((m) => m.GroupOrderCreateComponent),
      },
      {
        path: 'group-order/join',
        loadComponent: () =>
          import('./features/group-order/group-order-join.component').then((m) => m.GroupOrderJoinComponent),
      },
      {
        path: 'group-order/:id',
        loadComponent: () =>
          import('./features/group-order/group-order-view.component').then((m) => m.GroupOrderViewComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
