// ── Enums ─────────────────────────────────────────────────────────────────────

export enum OrderStatus {
  Created = 'Created',
  Accepted = 'Accepted',
  Preparing = 'Preparing',
  Ready = 'Ready',
  Completed = 'Completed',
  Declined = 'Declined',
}

export enum PaymentStatus {
  Unpaid = 'Unpaid',
  Paid = 'Paid',
  Refunded = 'Refunded',
}

export enum PaymentMethod {
  Cash = 'Cash',
  Card = 'Card',
  Other = 'Other',
}

// ── Auth ──────────────────────────────────────────────────────────────────────

export interface AuthResponse {
  token: string;
  userType: string;
  userId: string;
  displayName: string;
}

export interface CustomerProfile {
  id: string;
  mobileNumber: string;
  firstName: string;
  lastName: string;
  createdAt: string;
}

// ── Location ──────────────────────────────────────────────────────────────────

export interface Location {
  id: string;
  name: string;
  address: string;
  workingHours: string;
  contactPhone: string;
  isActive: boolean;
}

// ── Menu ──────────────────────────────────────────────────────────────────────

export interface MenuCategory {
  id: string;
  name: string;
  sortOrder: number;
}

export interface MenuItem {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string;
  price: number;
  isAvailable: boolean;
  imageUrl?: string;
}

// ── Orders ────────────────────────────────────────────────────────────────────

export interface OrderItemInput {
  menuItemId: string;
  quantity: number;
}

export interface CreateOrderRequest {
  locationId: string;
  items: OrderItemInput[];
}

export interface OrderItem {
  id: string;
  menuItemId: string;
  menuItemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  customerUserId: string;
  locationId: string;
  locationName: string;
  status: OrderStatus;
  declineReason?: string;
  etaMinutes?: number;
  createdAt: string;
  acceptedAt?: string;
  completedAt?: string;
  paymentStatus: PaymentStatus;
  paymentMethod?: PaymentMethod;
  subtotal: number;
  total: number;
  items: OrderItem[];
}

// ── Favorites ────────────────────────────────────────────────────────────────

export interface Favorite extends MenuItem {}

// ── Cart ─────────────────────────────────────────────────────────────────────

export interface CartItem {
  menuItemId: string;
  name: string;
  price: number;
  quantity: number;
  imageUrl?: string;
}

// ── Admin DTOs ───────────────────────────────────────────────────────────────

export interface AcceptOrderRequest {
  etaMinutes: number;
}

export interface DeclineOrderRequest {
  reason: string;
}

export interface UpdateStatusRequest {
  status: OrderStatus;
}

export interface UpdatePaymentRequest {
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
}

// ── Analytics ────────────────────────────────────────────────────────────────

export interface AnalyticsResponse {
  kpis: KpiSummary;
  revenueOverTime: RevenueDataPoint[];
  topItems: TopItemDto[];
  locationPerformance: LocationPerformanceDto[];
  hourlyDistribution: HourlyDistributionDto[];
  paymentBreakdown: PaymentBreakdownDto[];
  statusBreakdown: OrderStatusBreakdownDto[];
}

export interface KpiSummary {
  totalRevenue: number;
  totalOrders: number;
  avgOrderValue: number;
  completedOrders: number;
  declinedOrders: number;
  avgPrepTimeMinutes: number;
  uniqueCustomers: number;
}

export interface RevenueDataPoint {
  label: string;
  revenue: number;
  orders: number;
}

export interface TopItemDto {
  name: string;
  quantitySold: number;
  revenue: number;
}

export interface LocationPerformanceDto {
  locationName: string;
  revenue: number;
  orders: number;
}

export interface HourlyDistributionDto {
  hour: number;
  orders: number;
}

export interface PaymentBreakdownDto {
  method: string;
  count: number;
  total: number;
}

export interface OrderStatusBreakdownDto {
  status: string;
  count: number;
}

// ── Promotions ───────────────────────────────────────────────────────────────

export interface Promotion {
  id: string;
  title: string;
  description: string;
  imageUrl?: string;
  isActive: boolean;
  expiresAt?: string;
  createdAt: string;
}
