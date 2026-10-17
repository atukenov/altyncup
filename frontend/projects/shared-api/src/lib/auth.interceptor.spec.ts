import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthStateService } from './auth-state.service';
import { YurtApiService } from './yurt-api.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const auth = {
    token: 'old-access' as string | null,
    refreshToken: 'refresh-1' as string | null,
    currentUser: { userType: 'customer' },
    updateTokens: vi.fn(),
    logout: vi.fn(),
  };
  const api = {
    refreshToken: vi.fn(() => of({ accessToken: 'new-access', refreshToken: 'refresh-2' })),
    adminRefreshToken: vi.fn(),
  };

  beforeEach(() => {
    auth.token = 'old-access';
    auth.refreshToken = 'refresh-1';
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStateService, useValue: auth },
        { provide: YurtApiService, useValue: api },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('adds the bearer token', () => {
    http.get('/api/v1/orders/active').subscribe();
    const req = backend.expectOne('/api/v1/orders/active');
    expect(req.request.headers.get('Authorization')).toBe('Bearer old-access');
    req.flush([]);
  });

  it('refreshes once on 401 and retries with the new token', () => {
    let body: unknown;
    http.get('/api/v1/orders/active').subscribe((b) => (body = b));

    backend.expectOne('/api/v1/orders/active').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(api.refreshToken).toHaveBeenCalledWith('refresh-1');
    expect(auth.updateTokens).toHaveBeenCalledWith('new-access', 'refresh-2');

    const retry = backend.expectOne('/api/v1/orders/active');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-access');
    retry.flush(['ok']);
    expect(body).toEqual(['ok']);
  });

  it('logs out on 401 when there is no refresh token', () => {
    auth.refreshToken = null;
    http.get('/api/v1/orders/active').subscribe({ error: () => {} });
    backend.expectOne('/api/v1/orders/active').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.logout).toHaveBeenCalled();
    expect(api.refreshToken).not.toHaveBeenCalled();
  });
});
