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
  Pending = 'Pending',
  Paid = 'Paid',
  Failed = 'Failed',
  Expired = 'Expired',
  Refunded = 'Refunded',
}

export enum PaymentMethod {
  Cash = 'Cash',
  Card = 'Card',
  Other = 'Other',
  KaspiBank = 'KaspiBank',
  HalykBank = 'HalykBank',
  FreedomBank = 'FreedomBank',
}

export enum PaymentProvider {
  KaspiSandbox = 'KaspiSandbox',
  AiPay = 'AiPay',
  PayBot = 'PayBot',
  Halyk = 'Halyk',
  FreedomPay = 'FreedomPay',
  Stripe = 'Stripe',
  PayPal = 'PayPal',
}

export enum Currency {
  KZT = 'KZT',
  USD = 'USD',
  EUR = 'EUR',
}

export enum SandboxPaymentBehavior {
  Default = 'Default',
  Success = 'Success',
  Failure = 'Failure',
  Expired = 'Expired',
  DuplicateWebhook = 'DuplicateWebhook',
  NetworkFailure = 'NetworkFailure',
}

// ── Auth ──────────────────────────────────────────────────────────────────────

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  userType: string;
  userId: string;
  displayName: string;
  role?: string;
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
  nameRu?: string;
  nameKk?: string;
  address: string;
  workingHours: string;
  contactPhone: string;
  isActive: boolean;
}

// ── Menu ──────────────────────────────────────────────────────────────────────

export interface MenuCategory {
  id: string;
  name: string;
  nameRu?: string;
  nameKk?: string;
  sortOrder: number;
}

export interface MenuTopping {
  id: string;
  name: string;
  nameRu?: string;
  nameKk?: string;
  price: number;
  isAvailable: boolean;
  categoryIds: string[];
  group?: string | null;
}

export interface MenuItem {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  nameRu?: string;
  nameKk?: string;
  description: string;
  descriptionRu?: string;
  descriptionKk?: string;
  price: number;
  isAvailable: boolean;
  imageUrl?: string;
  availableToppings?: MenuTopping[];
}

// ── Orders ────────────────────────────────────────────────────────────────────

export interface OrderItemToppingInput {
  toppingId: string;
  toppingName: string;
  price: number;
}

export interface OrderItemInput {
  menuItemId: string;
  quantity: number;
  toppings?: OrderItemToppingInput[];
  notes?: string;
}

export interface CreateOrderRequest {
  locationId: string;
  items: OrderItemInput[];
  paymentMethod: PaymentMethod;
}

export interface CreatePaymentRequest {
  orderId: string;
  provider: PaymentProvider;
  sandboxBehavior?: SandboxPaymentBehavior;
}

export interface PaymentInvoiceResponse {
  paymentId: string;
  invoiceId: string;
  paymentUrl: string;
  qrCode: string;
  amount: number;
  currency: Currency;
  provider: PaymentProvider;
  status: PaymentStatus;
  createdAt: string;
  expiresAt?: string;
  rawResponse?: string;
}

export interface PaymentStatusResponse {
  invoiceId: string;
  status: PaymentStatus;
  orderId: string;
  orderStatus: string;
  amount: number;
  currency: Currency;
  provider: PaymentProvider;
  paymentUrl: string;
  qrCode: string;
  createdAt: string;
  updatedAt?: string;
  rawResponse?: string;
}

export interface OrderItemTopping {
  toppingId: string;
  toppingName: string;
  price: number;
}

export interface OrderItem {
  id: string;
  menuItemId: string;
  menuItemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  toppings: OrderItemTopping[];
  notes?: string;
}

export interface Order {
  id: string;
  customerUserId: string;
  customerName: string;
  customerPhone?: string;
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
  selectedToppings?: OrderItemToppingInput[];
  notes?: string;
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

// ── Workers ───────────────────────────────────────────────────────────────────

export interface WorkerAccount {
  id: string;
  username: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}

// ── Customers (admin view) ────────────────────────────────────────────────────

export interface CustomerSummary {
  id: string;
  phone: string;
  displayName: string;
  registeredAt: string;
  orderCount: number;
  totalSpent: number;
}

export interface CustomerOrderSummary {
  id: string;
  createdAt: string;
  status: OrderStatus;
  total: number;
  locationName: string;
  itemCount: number;
}

export interface CustomerDetail {
  id: string;
  phone: string;
  displayName?: string;
  registeredAt: string;
  isActive: boolean;
  totalOrders: number;
  totalSpent: number;
  recentOrders: CustomerOrderSummary[];
}

// ── Group Ordering ────────────────────────────────────────────────────────────

export enum GroupCartStatus {
  Open = 'Open',
  Finalized = 'Finalized',
  Expired = 'Expired',
}

export interface GroupCartMember {
  customerId: string;
  displayName: string;
}

export interface GroupCartItem {
  id: string;
  addedByUserId: string;
  addedByName: string;
  menuItemId: string;
  menuItemName: string;
  unitPrice: number;
  quantity: number;
  toppings: OrderItemToppingInput[];
  notes?: string;
}

export interface GroupCart {
  id: string;
  code: string;
  locationName: string;
  locationId: string;
  status: GroupCartStatus;
  expiresAt: string;
  isCreator: boolean;
  members: GroupCartMember[];
  items: GroupCartItem[];
}

export interface AddGroupOrderItemRequest {
  menuItemId: string;
  menuItemName: string;
  unitPrice: number;
  quantity: number;
  toppings: OrderItemToppingInput[];
  notes?: string;
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

export interface DashboardData {
  ordersToday: number;
  revenueToday: number;
  avgOrderValue: number;
  pendingCount: number;
  hourlyOrders: { hour: number; count: number }[];
}

// ── Customer stats ────────────────────────────────────────────────────────────

export interface CustomerStats {
  totalOrders: number;
  totalSpent: number;
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


export interface AuditLogEntry {
  id: string;
  action: string;
  entityType: string;
  entityId?: string;
  performedByAdminId?: string;
  performedByUsername: string;
  details?: string;
  ipAddress?: string;
  createdAt: string;
}
