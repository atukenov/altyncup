import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AcceptOrderRequest,
  AnalyticsResponse,
  AuthResponse,
  CreateOrderRequest,
  CreatePaymentRequest,
  CustomerProfile,
  CustomerStats,
  CustomerSummary,
  DashboardData,
  DeclineOrderRequest,
  Location,
  MenuCategory,
  MenuItem,
  MenuTopping,
  Order,
  OrderStatus,
  PaymentInvoiceResponse,
  PaymentStatusResponse,
  Promotion,
  UpdatePaymentRequest,
  UpdateStatusRequest,
  WorkerAccount,
} from 'shared-models';

@Injectable({ providedIn: 'root' })
export class YurtApiService {
  private readonly http = inject(HttpClient);
  private baseUrl = '';

  configure(baseUrl: string): void {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  // ── Auth ───────────────────────────────────────────────────────────────────
  register(
    mobileNumber: string,
    pin4: string,
    firstName: string = '',
    lastName: string = '',
  ): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/auth/register`, {
      mobileNumber,
      pin4,
      firstName,
      lastName,
    });
  }

  login(mobileNumber: string, pin4: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/auth/login`, { mobileNumber, pin4 });
  }

  me(): Observable<CustomerProfile> {
    return this.http.get<CustomerProfile>(`${this.baseUrl}/api/auth/me`);
  }

  updateProfile(data: { firstName: string; lastName: string }): Observable<CustomerProfile> {
    return this.http.put<CustomerProfile>(`${this.baseUrl}/api/auth/me`, data);
  }

  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/auth/refresh`, {});
  }

  adminLogin(username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/admin/auth/login`, {
      username,
      password,
    });
  }

  // ── Locations ──────────────────────────────────────────────────────────────
  getLocations(): Observable<Location[]> {
    return this.http.get<Location[]>(`${this.baseUrl}/api/locations`);
  }

  getAdminLocations(): Observable<Location[]> {
    return this.http.get<Location[]>(`${this.baseUrl}/api/admin/locations`);
  }

  createLocation(data: Partial<Location>): Observable<Location> {
    return this.http.post<Location>(`${this.baseUrl}/api/admin/locations`, data);
  }

  updateLocation(id: string, data: Partial<Location>): Observable<Location> {
    return this.http.put<Location>(`${this.baseUrl}/api/admin/locations/${id}`, data);
  }

  deleteLocation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/admin/locations/${id}`);
  }

  // ── Menu ───────────────────────────────────────────────────────────────────
  getCategories(): Observable<MenuCategory[]> {
    return this.http.get<MenuCategory[]>(`${this.baseUrl}/api/menu/categories`);
  }

  getMenuItems(categoryId?: string, search?: string): Observable<MenuItem[]> {
    let params = new HttpParams();
    if (categoryId) params = params.set('categoryId', categoryId);
    if (search) params = params.set('search', search);
    return this.http.get<MenuItem[]>(`${this.baseUrl}/api/menu`, { params });
  }

  getMenuItem(id: string): Observable<MenuItem> {
    return this.http.get<MenuItem>(`${this.baseUrl}/api/menu/items/${id}`);
  }

  // Admin menu
  adminGetMenuItems(): Observable<MenuItem[]> {
    return this.http.get<MenuItem[]>(`${this.baseUrl}/api/admin/menu/items`);
  }

  adminCreateMenuItem(data: Partial<MenuItem>): Observable<MenuItem> {
    return this.http.post<MenuItem>(`${this.baseUrl}/api/admin/menu/items`, data);
  }

  adminUpdateMenuItem(id: string, data: Partial<MenuItem>): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.baseUrl}/api/admin/menu/items/${id}`, data);
  }

  adminDeleteMenuItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/admin/menu/items/${id}`);
  }

  adminCreateCategory(data: Partial<MenuCategory>): Observable<MenuCategory> {
    return this.http.post<MenuCategory>(`${this.baseUrl}/api/admin/menu/categories`, data);
  }

  adminUpdateCategory(id: string, data: Partial<MenuCategory>): Observable<MenuCategory> {
    return this.http.put<MenuCategory>(`${this.baseUrl}/api/admin/menu/categories/${id}`, data);
  }

  adminDeleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/admin/menu/categories/${id}`);
  }

  adminGetToppings(): Observable<MenuTopping[]> {
    return this.http.get<MenuTopping[]>(`${this.baseUrl}/api/admin/menu/toppings`);
  }

  adminCreateTopping(data: Partial<MenuTopping>): Observable<MenuTopping> {
    return this.http.post<MenuTopping>(`${this.baseUrl}/api/admin/menu/toppings`, data);
  }

  adminUpdateTopping(id: string, data: Partial<MenuTopping>): Observable<MenuTopping> {
    return this.http.put<MenuTopping>(`${this.baseUrl}/api/admin/menu/toppings/${id}`, data);
  }

  adminDeleteTopping(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/admin/menu/toppings/${id}`);
  }

  getToppings(categoryId?: string): Observable<MenuTopping[]> {
    const params = categoryId ? new HttpParams().set('categoryId', categoryId) : undefined;
    return this.http.get<MenuTopping[]>(`${this.baseUrl}/api/menu/toppings`, { params });
  }

  // ── Favorites ──────────────────────────────────────────────────────────────
  getFavorites(): Observable<MenuItem[]> {
    return this.http.get<MenuItem[]>(`${this.baseUrl}/api/favorites`);
  }

  addFavorite(menuItemId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/favorites/${menuItemId}`, {});
  }

  removeFavorite(menuItemId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/favorites/${menuItemId}`);
  }

  // ── Orders ─────────────────────────────────────────────────────────────────
  createOrder(request: CreateOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.baseUrl}/api/orders`, request);
  }

  getActiveOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.baseUrl}/api/orders/active`);
  }

  createPayment(request: CreatePaymentRequest): Observable<PaymentInvoiceResponse> {
    return this.http.post<PaymentInvoiceResponse>(`${this.baseUrl}/api/payments/create`, request);
  }

  getPaymentStatus(invoiceId: string): Observable<PaymentStatusResponse> {
    return this.http.get<PaymentStatusResponse>(`${this.baseUrl}/api/payments/status/${invoiceId}`);
  }

  getOrderHistory(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.baseUrl}/api/orders/history`);
  }

  getDeclinedOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.baseUrl}/api/orders/declined`);
  }

  getOrder(id: string): Observable<Order> {
    return this.http.get<Order>(`${this.baseUrl}/api/orders/${id}`);
  }

  // Admin orders
  getAdminOrders(status?: OrderStatus, locationId?: string): Observable<Order[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<Order[]>(`${this.baseUrl}/api/admin/orders`, { params });
  }

  acceptOrder(id: string, req: AcceptOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.baseUrl}/api/admin/orders/${id}/accept`, req);
  }

  declineOrder(id: string, req: DeclineOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.baseUrl}/api/admin/orders/${id}/decline`, req);
  }

  updateOrderStatus(id: string, req: UpdateStatusRequest): Observable<Order> {
    return this.http.post<Order>(`${this.baseUrl}/api/admin/orders/${id}/status`, req);
  }

  updateOrderPayment(id: string, req: UpdatePaymentRequest): Observable<Order> {
    return this.http.post<Order>(`${this.baseUrl}/api/admin/orders/${id}/payment`, req);
  }

  // ── Dashboard ──────────────────────────────────────────────────────────────
  getDashboard(): Observable<DashboardData> {
    return this.http.get<DashboardData>(`${this.baseUrl}/api/admin/dashboard`);
  }

  // ── Customers (admin) ──────────────────────────────────────────────────────
  getAdminCustomers(phone?: string): Observable<CustomerSummary[]> {
    const params = phone ? new HttpParams().set('phone', phone) : undefined;
    return this.http.get<CustomerSummary[]>(`${this.baseUrl}/api/admin/customers`, { params });
  }

  // ── Workers ────────────────────────────────────────────────────────────────
  getWorkers(): Observable<WorkerAccount[]> {
    return this.http.get<WorkerAccount[]>(`${this.baseUrl}/api/admin/workers`);
  }

  createWorker(data: { username: string; password: string }): Observable<WorkerAccount> {
    return this.http.post<WorkerAccount>(`${this.baseUrl}/api/admin/workers`, data);
  }

  updateWorker(id: string, data: { username: string; isActive: boolean }): Observable<WorkerAccount> {
    return this.http.put<WorkerAccount>(`${this.baseUrl}/api/admin/workers/${id}`, data);
  }

  resetWorkerPassword(id: string, newPassword: string): Observable<WorkerAccount> {
    return this.http.post<WorkerAccount>(`${this.baseUrl}/api/admin/workers/${id}/reset-password`, { newPassword });
  }

  // ── Customer account ────────────────────────────────────────────────────────
  changePin(currentPin: string, newPin: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/api/auth/pin`, { currentPin, newPin });
  }

  deleteAccount(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/auth/me`);
  }

  getCustomerStats(): Observable<CustomerStats> {
    return this.http.get<CustomerStats>(`${this.baseUrl}/api/auth/me/stats`);
  }

  // ── Analytics ───────────────────────────────────────────────────────────────
  getAnalytics(period: string): Observable<AnalyticsResponse> {
    const params = new HttpParams().set('period', period);
    return this.http.get<AnalyticsResponse>(`${this.baseUrl}/api/admin/analytics`, { params });
  }

  // ── Promotions ──────────────────────────────────────────────────────────────
  getActivePromotions(): Observable<Promotion[]> {
    return this.http.get<Promotion[]>(`${this.baseUrl}/api/promotions/active`);
  }

  getAdminPromotions(): Observable<Promotion[]> {
    return this.http.get<Promotion[]>(`${this.baseUrl}/api/admin/promotions`);
  }

  createPromotion(data: Partial<Promotion>): Observable<Promotion> {
    return this.http.post<Promotion>(`${this.baseUrl}/api/admin/promotions`, data);
  }

  updatePromotion(id: string, data: Partial<Promotion>): Observable<Promotion> {
    return this.http.put<Promotion>(`${this.baseUrl}/api/admin/promotions/${id}`, data);
  }

  deletePromotion(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/admin/promotions/${id}`);
  }
}
