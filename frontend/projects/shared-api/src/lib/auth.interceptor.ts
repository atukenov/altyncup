import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { YurtApiService } from './yurt-api.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStateService);
  const api = inject(YurtApiService);

  const authReq = addToken(req, auth.token);

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !req.url.includes('/auth/refresh') && !req.url.includes('/auth/logout')) {
        const rt = auth.refreshToken;
        if (rt) {
          const isAdmin = auth.currentUser?.userType === 'admin';
          const refresh$ = isAdmin ? api.adminRefreshToken(rt) : api.refreshToken(rt);
          return refresh$.pipe(
            switchMap(res => {
              auth.updateTokens(res.accessToken, res.refreshToken);
              return next(addToken(req, res.accessToken));
            }),
            catchError(refreshErr => {
              if (refreshErr.status === 401 || refreshErr.status === 403) {
                auth.logout();
              }
              return throwError(() => refreshErr);
            }),
          );
        }
        auth.logout();
      }
      return throwError(() => err);
    }),
  );
};

function addToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}
