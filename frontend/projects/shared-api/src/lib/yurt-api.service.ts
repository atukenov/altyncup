import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AcceptOrderRequest,
  AddGroupOrderItemRequest,
  AnalyticsResponse,
  AuditLogEntry,
  AuthResponse,
  CreateOrderRequest,
  CreatePaymentRequest,
  CustomerDetail,
  CustomerProfile,
  CustomerStats,
  CustomerSummary,
  DashboardData,
  DeclineOrderRequest,
  DiscountCode,
  GroupCart,
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
  ValidateDiscountCodeResponse,
  WorkerAccount,
} from 'shared-models';

@Injectable({ providedIn: 'root' })
export class YurtApiService {
  private readonly http = inject(HttpClient);
  private baseUrl = '';

  configure(baseUrl: string): void {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  private get api(): string {
    return `${this.baseUrl}/api/v1`;
  }

  // ── Auth ───────────────────────────────────────────────────────────────────
  register(
    mobileNumber: string,
    pin4: string,
    firstName: string = '',
    lastName: string = '',
  ): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/register`, {
      mobileNumber,
      pin4,
      firstName,
      lastName,
    });
  }

  login(mobileNumber: string, pin4: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/login`, { mobileNumber, pin4 });
  }

  me(): Observable<CustomerProfile> {
    return this.http.get<CustomerProfile>(`${this.api}/auth/me`);
  }

  updateProfile(data: { firstName: string; lastName: string }): Observable<CustomerProfile> {
    return this.http.put<CustomerProfile>(`${this.api}/auth/me`, data);
  }

  refreshToken(refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/refresh`, { refreshToken });
  }

  adminRefreshToken(refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/admin/auth/refresh`, { refreshToken });
  }

  logout(refreshToken: string): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/logout`, { refreshToken });
  }

  adminLogout(refreshToken: string): Observable<void> {
    return this.http.post<void>(`${this.api}/admin/auth/logout`, { refreshToken });
  }

  adminLogin(username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/admin/auth/login`, {
      username,
      password,
    });
  }

  // ── Locations ──────────────────────────────────────────────────────────────
  getLocations(lang?: string): Observable<Location[]> {
    const params = lang ? new HttpParams().set('lang', lang) : undefined;
    return this.http.get<Location[]>(`${this.api}/locations`, { params });
  }

  getAdminLocations(): Observable<Location[]> {
    return this.http.get<Location[]>(`${this.api}/admin/locations`);
  }

  createLocation(data: Partial<Location>): Observable<Location> {
    return this.http.post<Location>(`${this.api}/admin/locations`, data);
  }

  updateLocation(id: string, data: Partial<Location>): Observable<Location> {
    return this.http.put<Location>(`${this.api}/admin/locations/${id}`, data);
  }

  deleteLocation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/locations/${id}`);
  }

  // ── Menu ───────────────────────────────────────────────────────────────────
  getCategories(lang?: string): Observable<MenuCategory[]> {
    const params = lang ? new HttpParams().set('lang', lang) : undefined;
    return this.http.get<MenuCategory[]>(`${this.api}/menu/categories`, { params });
  }

  getMenuItems(categoryId?: string, search?: string, lang?: string, locationId?: string): Observable<MenuItem[]> {
    let params = new HttpParams();
    if (categoryId) params = params.set('categoryId', categoryId);
    if (search) params = params.set('search', search);
    if (lang) params = params.set('lang', lang);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<MenuItem[]>(`${this.api}/menu`, { params });
  }

  getMenuItem(id: string, lang?: string, locationId?: string): Observable<MenuItem> {
    let params = new HttpParams();
    if (lang) params = params.set('lang', lang);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<MenuItem>(`${this.api}/menu/items/${id}`, { params });
  }

  // Admin menu
  adminGetMenuItems(): Observable<MenuItem[]> {
    return this.http.get<MenuItem[]>(`${this.api}/admin/menu/items`);
  }

  adminCreateMenuItem(data: Partial<MenuItem>): Observable<MenuItem> {
    return this.http.post<MenuItem>(`${this.api}/admin/menu/items`, data);
  }

  adminUpdateMenuItem(id: string, data: Partial<MenuItem>): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.api}/admin/menu/items/${id}`, data);
  }

  adminDeleteMenuItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/menu/items/${id}`);
  }

  adminCreateCategory(data: Partial<MenuCategory>): Observable<MenuCategory> {
    return this.http.post<MenuCategory>(`${this.api}/admin/menu/categories`, data);
  }

  adminUpdateCategory(id: string, data: Partial<MenuCategory>): Observable<MenuCategory> {
    return this.http.put<MenuCategory>(`${this.api}/admin/menu/categories/${id}`, data);
  }

  adminDeleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/menu/categories/${id}`);
  }

  adminGetToppings(): Observable<MenuTopping[]> {
    return this.http.get<MenuTopping[]>(`${this.api}/admin/menu/toppings`);
  }

  adminCreateTopping(data: Partial<MenuTopping>): Observable<MenuTopping> {
    return this.http.post<MenuTopping>(`${this.api}/admin/menu/toppings`, data);
  }

  adminUpdateTopping(id: string, data: Partial<MenuTopping>): Observable<MenuTopping> {
    return this.http.put<MenuTopping>(`${this.api}/admin/menu/toppings/${id}`, data);
  }

  adminDeleteTopping(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/menu/toppings/${id}`);
  }

  getToppings(categoryId?: string, lang?: string): Observable<MenuTopping[]> {
    let params = new HttpParams();
    if (categoryId) params = params.set('categoryId', categoryId);
    if (lang) params = params.set('lang', lang);
    return this.http.get<MenuTopping[]>(`${this.api}/menu/toppings`, { params });
  }

  // ── Favorites ──────────────────────────────────────────────────────────────
  getFavorites(lang?: string): Observable<MenuItem[]> {
    const params = lang ? new HttpParams().set('lang', lang) : undefined;
    return this.http.get<MenuItem[]>(`${this.api}/favorites`, { params });
  }

  addFavorite(menuItemId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/favorites/${menuItemId}`, {});
  }

  removeFavorite(menuItemId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/favorites/${menuItemId}`);
  }

  // ── Orders ─────────────────────────────────────────────────────────────────
  createOrder(request: CreateOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/orders`, request);
  }

  getActiveOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.api}/orders/active`);
  }

  createPayment(request: CreatePaymentRequest): Observable<PaymentInvoiceResponse> {
    return this.http.post<PaymentInvoiceResponse>(`${this.api}/payments/create`, request);
  }

  getPaymentStatus(invoiceId: string): Observable<PaymentStatusResponse> {
    return this.http.get<PaymentStatusResponse>(`${this.api}/payments/status/${invoiceId}`);
  }

  getOrderHistory(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.api}/orders/history`);
  }

  getDeclinedOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.api}/orders/declined`);
  }

  getOrder(id: string): Observable<Order> {
    return this.http.get<Order>(`${this.api}/orders/${id}`);
  }

  // Admin orders
  getAdminOrders(status?: OrderStatus, locationId?: string): Observable<Order[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<Order[]>(`${this.api}/admin/orders`, { params });
  }

  acceptOrder(id: string, req: AcceptOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/accept`, req);
  }

  declineOrder(id: string, req: DeclineOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/decline`, req);
  }

  updateOrderStatus(id: string, req: UpdateStatusRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/status`, req);
  }

  updateOrderPayment(id: string, req: UpdatePaymentRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/payment`, req);
  }

  // ── Dashboard ──────────────────────────────────────────────────────────────
  getDashboard(): Observable<DashboardData> {
    return this.http.get<DashboardData>(`${this.api}/admin/dashboard`);
  }

  // ── Customers (admin) ──────────────────────────────────────────────────────
  getAdminCustomers(phone?: string): Observable<CustomerSummary[]> {
    const params = phone ? new HttpParams().set('phone', phone) : undefined;
    return this.http.get<CustomerSummary[]>(`${this.api}/admin/customers`, { params });
  }

  getAdminCustomer(id: string): Observable<CustomerDetail> {
    return this.http.get<CustomerDetail>(`${this.api}/admin/customers/${id}`);
  }

  setCustomerActive(id: string, isActive: boolean): Observable<CustomerDetail> {
    return this.http.patch<CustomerDetail>(`${this.api}/admin/customers/${id}/active`, { isActive });
  }

  // ── Workers ────────────────────────────────────────────────────────────────
  getWorkers(): Observable<WorkerAccount[]> {
    return this.http.get<WorkerAccount[]>(`${this.api}/admin/workers`);
  }

  createWorker(data: { username: string; password: string }): Observable<WorkerAccount> {
    return this.http.post<WorkerAccount>(`${this.api}/admin/workers`, data);
  }

  updateWorker(id: string, data: { username: string; isActive: boolean }): Observable<WorkerAccount> {
    return this.http.put<WorkerAccount>(`${this.api}/admin/workers/${id}`, data);
  }

  setWorkerActive(id: string, isActive: boolean): Observable<WorkerAccount> {
    return this.http.patch<WorkerAccount>(`${this.api}/admin/workers/${id}/active`, { isActive });
  }

  resetWorkerPassword(id: string, newPassword: string): Observable<WorkerAccount> {
    return this.http.post<WorkerAccount>(`${this.api}/admin/workers/${id}/reset-password`, { newPassword });
  }

  // ── Customer account ────────────────────────────────────────────────────────
  changePin(currentPin: string, newPin: string): Observable<void> {
    return this.http.put<void>(`${this.api}/auth/pin`, { currentPin, newPin });
  }

  deleteAccount(): Observable<void> {
    return this.http.delete<void>(`${this.api}/auth/me`);
  }

  getCustomerStats(): Observable<CustomerStats> {
    return this.http.get<CustomerStats>(`${this.api}/auth/me/stats`);
  }

  // ── Analytics ───────────────────────────────────────────────────────────────
  getAnalytics(period: string): Observable<AnalyticsResponse> {
    const params = new HttpParams().set('period', period);
    return this.http.get<AnalyticsResponse>(`${this.api}/admin/analytics`, { params });
  }

  // ── Promotions ──────────────────────────────────────────────────────────────
  getActivePromotions(): Observable<Promotion[]> {
    return this.http.get<Promotion[]>(`${this.api}/promotions/active`);
  }

  getAdminPromotions(): Observable<Promotion[]> {
    return this.http.get<Promotion[]>(`${this.api}/admin/promotions`);
  }

  createPromotion(data: Partial<Promotion>): Observable<Promotion> {
    return this.http.post<Promotion>(`${this.api}/admin/promotions`, data);
  }

  updatePromotion(id: string, data: Partial<Promotion>): Observable<Promotion> {
    return this.http.put<Promotion>(`${this.api}/admin/promotions/${id}`, data);
  }

  deletePromotion(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/promotions/${id}`);
  }

  // ── Discount Codes ──────────────────────────────────────────────────────────
  getAdminDiscountCodes(): Observable<DiscountCode[]> {
    return this.http.get<DiscountCode[]>(`${this.api}/admin/discount-codes`);
  }

  createDiscountCode(data: Partial<DiscountCode>): Observable<DiscountCode> {
    return this.http.post<DiscountCode>(`${this.api}/admin/discount-codes`, data);
  }

  updateDiscountCode(id: string, data: Partial<DiscountCode>): Observable<DiscountCode> {
    return this.http.put<DiscountCode>(`${this.api}/admin/discount-codes/${id}`, data);
  }

  deleteDiscountCode(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/discount-codes/${id}`);
  }

  validateDiscountCode(code: string, subtotal: number): Observable<ValidateDiscountCodeResponse> {
    return this.http.post<ValidateDiscountCodeResponse>(`${this.api}/discount-codes/validate`, { code, subtotal });
  }

  // ── Audit Log ───────────────────────────────────────────────────────────────
  getAuditLog(params: {
    entityType?: string;
    adminId?: string;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Observable<{ total: number; page: number; pageSize: number; items: AuditLogEntry[] }> {
    let httpParams = new HttpParams();
    if (params.entityType) httpParams = httpParams.set('entityType', params.entityType);
    if (params.adminId) httpParams = httpParams.set('adminId', params.adminId);
    if (params.from) httpParams = httpParams.set('from', params.from);
    if (params.to) httpParams = httpParams.set('to', params.to);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<{ total: number; page: number; pageSize: number; items: AuditLogEntry[] }>(
      `${this.api}/admin/audit-log`, { params: httpParams });
  }

  // ── Group Orders ────────────────────────────────────────────────────────────
  createGroupOrder(locationId: string): Observable<GroupCart> {
    return this.http.post<GroupCart>(`${this.api}/group-orders`, { locationId });
  }

  joinGroupOrder(code: string): Observable<GroupCart> {
    return this.http.post<GroupCart>(`${this.api}/group-orders/join`, { code });
  }

  getGroupOrder(id: string): Observable<GroupCart> {
    return this.http.get<GroupCart>(`${this.api}/group-orders/${id}`);
  }

  addGroupOrderItem(id: string, req: AddGroupOrderItemRequest): Observable<GroupCart> {
    return this.http.post<GroupCart>(`${this.api}/group-orders/${id}/items`, req);
  }

  removeGroupOrderItem(id: string, itemId: string): Observable<GroupCart> {
    return this.http.delete<GroupCart>(`${this.api}/group-orders/${id}/items/${itemId}`);
  }

  checkoutGroupOrder(id: string): Observable<Order> {
    return this.http.post<Order>(`${this.api}/group-orders/${id}/checkout`, {});
  }
}
