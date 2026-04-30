import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuthResponse,
  CustomerProfile,
  Location,
  MenuCategory,
  MenuItem,
  Order,
  CreateOrderRequest,
  AcceptOrderRequest,
  DeclineOrderRequest,
  UpdateStatusRequest,
  UpdatePaymentRequest,
  OrderStatus,
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
}
