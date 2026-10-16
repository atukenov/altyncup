import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from 'shared-api';

export const adminAuthGuard: CanActivateChildFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  if (auth.isLoggedIn && auth.currentUser?.userType === 'admin') return true;
  return router.createUrlTree(['/auth/login']);
};

/** Cafe sections (menu, locations, promotions, ...): Admin or SuperAdmin. Workers go back to orders. */
export const cafeAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  return auth.isAdmin || inject(Router).createUrlTree(['/orders']);
};

/** IT sections (app releases, audit log): SuperAdmin only. */
export const superAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  return auth.isSuperAdmin || inject(Router).createUrlTree(['/orders']);
};
