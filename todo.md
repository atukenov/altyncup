# TODO

## ✅ Completed

- [x] Login / Register phone input
  - Always display the phone code prefix `+7` in the mobile phone field.
  - User should enter only the 10-digit number after `+7`.
  - Validate the input format as `+7` followed by 10 digits.

- [x] PIN login behavior
  - When the user types a 4-digit PIN, automatically attempt sign in.
  - If the PIN is correct, proceed to login without requiring the user to press the "Sign in" button.
  - If the PIN is incorrect, show an error message.

- [x] Menu item toppings / add-ons feature
  - For menu categories like `Coffee` and `Cold Drinks`, show optional toppings/additional items when adding an item.
  - Examples: oat milk, almond milk, no sugar, extra hot, etc.
  - Store all toppings and additional items in the database.
  - Manage toppings/add-ons from the "Menu Management" screen.

- [x] Persistent customer login
  - When a customer logs in, save the session so the customer remains logged in.
  - The login should expire only after one week of inactivity.
  - If the customer logs in again within a week, refresh the expiration for another week.

- [x] Company Logo
  - Update favicon of the application and change logo.png as logo for the company.

- [x] Topping groups (mutual exclusion)
  - Toppings in the same group (e.g. "milk", "syrup") are rendered as radio buttons — only one can be selected at a time.
  - Free-pick toppings (no group) remain checkboxes.

- [x] Admin menu management — topping group field
  - Toppings can be assigned a group key ("milk", "syrup") from the admin panel.
  - Group is shown as a pill in the toppings table.

- [x] Customer order detail — toppings visible
  - Each order item in the detail view shows its selected toppings with name and price.

- [x] Customer order detail — receipt for completed orders
  - "View Receipt" button appears on completed orders.
  - Tapping it opens a bottom-sheet receipt with full item breakdown, toppings, totals, and payment info.

- [x] Dashboard with live metrics
  - Real-time cards: orders today, revenue today, average order value, pending orders count.
  - Simple bar chart of orders by hour for the current day.
  - Quick-action buttons: view all pending, go to menu.

- [x] Worker accounts & role-based access
  - Workers can only see and manage orders — no access to menu, promotions, or analytics.
  - Admins can create, deactivate, and reset passwords for worker accounts.

- [x] Home screen improvements
  - Personalized greeting with customer's first name.
  - "Order again" shortcut: re-add the last completed order to cart in one tap.

- [x] Cart & checkout enhancements
  - Cart persists across app sessions (saved locally).
  - Item quantity adjustment (+ / −) directly in the cart.
  - Special instructions text field per item.

- [x] Favorites
  - Heart icon on each menu item card and detail page.
  - Dedicated Favorites tab for quick reorder.

- [x] Push notifications
  - Notify customer when their order status changes.
  - Opt-in/opt-out from the profile screen.
  - Deep-link to the order detail page.

- [x] Profile & account settings
  - Edit display name, change PIN, view lifetime stats, delete account.

- [x] Rate limiting
  - Per-IP rate limits on auth endpoints.

- [x] Order archival
  - Orders older than 90 days are archived. Archive job runs nightly.

- [x] Language change
  - Application supports Russian, English, and Kazakh.
  - Language switcher on the profile page. Selection persists across sessions.

- [x] Customer list (admin)
  - View all registered customers with registration date, order count, and total spend.
  - Search by phone number.

- [x] **Analytics page**
  - Top-selling items (last 7 / 30 days) with quantity and revenue.
  - Revenue over time (daily/weekly/monthly) chart.
  - Peak hours heatmap.
  - Export to CSV.

- [x] **Location management**
  - Edit working hours, contact phone, and active status from the admin panel.
  - Temporarily close a location (orders blocked while inactive).

- [x] **Customer detail page (admin)**
  - Click into any customer from the customer list to see their full order history.
  - Profile card with total orders and total spend.

- [x] **Group ordering (customer)**
  - Customer creates a group cart and shares a link/code with friends.
  - Each participant adds their own items.
  - One person confirms and pays.

- [x] **Admin multi-language UI**
  - Admin panel fully translated into Russian, English, and Kazakh.
  - Language switcher (RU / EN / KK) in the admin sidebar.
  - All order status labels, navigation, form fields, and action buttons translated.
  - Single-language-at-a-time form for entering menu content translations.

---

## 🔲 Upcoming Features

### Admin Panel

- [ ] **Order management improvements**
  - Bulk-accept / bulk-decline multiple orders at once with a shared ETA or reason.
  - Filter orders by status, location, date range, and payment method.
  - Search orders by order number or customer phone number.
  - Print-friendly order ticket view (for kitchen display or receipt printer).

- [ ] **Menu management enhancements**
  - Drag-and-drop reordering for categories and items.
  - Bulk availability toggle: mark an entire category as unavailable (e.g. "Sold out today").
  - Item image upload directly from the admin panel instead of pasting a URL.
  - Duplicate item: copy an existing item as a starting point for a new one.

- [ ] **Promotions & discount codes**
  - Create time-limited or usage-limited discount codes (percentage or fixed amount).
  - Assign promotions to specific categories, items, or all orders.
  - View usage stats per promotion: how many times used, total discount given.

- [ ] **Staff performance metrics**
  - Per-worker stats: orders accepted, avg acceptance time, declined count.
  - Leaderboard view for the current day / week.
  - Useful for shift reviews and identifying bottlenecks.

- [ ] **Inventory / stock management**
  - Mark a menu item or topping as "out of stock" — auto-hides it from customers.
  - Optional stock counter per item; auto-hides when count reaches zero.
  - Low-stock alert shown on the dashboard.

- [ ] **Feedback moderation**
  - View all submitted ratings and comments.
  - Flag or respond to feedback.
  - Summary score per item and per location.

- [ ] **Audit log**
  - Track which worker accepted, declined, or completed each order.
  - Track admin actions: menu changes, worker account changes, promotions created.

---

### Customer Mobile App

- [ ] **Order rating & feedback**
  - After an order is marked Completed, prompt the customer to rate it (1–5 stars).
  - Optional short text comment.
  - Ratings visible to admins in the order detail and analytics.

- [ ] **Loyalty points**
  - Earn 1 point per ₸10 spent.
  - Points balance shown on the profile screen.
  - Redeem points for a discount at checkout (e.g. 100 points = ₸100 off).
  - Points history list (earned / redeemed per order).

- [ ] **Order scheduling**
  - Choose a pick-up time slot up to 2 hours in advance.
  - Order enters the queue only when the prep window approaches.

- [ ] **Referral program**
  - Each customer gets a unique referral code.
  - Sharing the code gives both the referrer and the new customer a one-time discount.
  - Referral stats visible on the profile screen.

- [ ] **Allergen & dietary filter**
  - Tag menu items with allergen labels (gluten, dairy, nuts, etc.) and dietary badges (vegan, vegetarian).
  - Filter bar on the menu screen: "Show only vegan", "Hide nuts".
  - Managed from the admin menu panel.

- [ ] **QR code location selection**
  - Each café table or counter has a QR code that deep-links to the app and pre-selects the location.
  - Eliminates the "Choose Location" step for in-café customers.
  - Admin generates printable QR codes per location.

- [ ] **Wallet / prepaid balance**
  - Customer tops up an in-app balance (e.g. ₸5,000 blocks).
  - Pay instantly at checkout using wallet — no Kaspi QR scan needed.
  - Wallet top-up via Kaspi QR. Balance shown on profile and cart.

- [ ] **Item availability notifications**
  - Customer taps "Notify me" on an out-of-stock item.
  - Push notification sent when the item becomes available again.

- [ ] **Birthday reward**
  - Customer sets birthday in profile.
  - On their birthday, automatically apply a free-item or discount coupon valid for 24 hours.

- [ ] **Social sharing**
  - Share a menu item card (image + name + price) to WhatsApp or Instagram Stories.
  - Share a completed receipt as an image.

---

### Backend & Infrastructure

- [ ] **Refresh token rotation**
  - Issue short-lived access tokens (15 min) and long-lived refresh tokens (7 days).
  - Rotate refresh token on each use; invalidate old token immediately.
  - Endpoint: `POST /api/auth/refresh`.

- [ ] **Background job queue**
  - Use Hangfire or a lightweight in-process queue for async work: sending push notifications, expiring promotions, generating daily analytics snapshots.

- [ ] **Structured logging & error tracking**
  - Enrich Serilog with request ID, user ID, and order ID context.
  - Ship logs to a sink (Seq, Grafana Loki, or similar).
  - Capture unhandled exceptions to an error-tracking service (Sentry).

- [ ] **Database migrations safety**
  - Startup check that warns if unapplied migrations exist in Production.
  - Separate migration runner from the web app (init container pattern).

- [ ] **Health check endpoint**
  - `GET /health` returns DB connectivity, SignalR hub status, and queue depth.
  - Integrated with Docker `HEALTHCHECK` directive.

- [ ] **API versioning**
  - Version the public API (`/api/v1/...`) so mobile and backend can be updated independently.

- [ ] **Payment provider expansion**
  - Abstract payment behind an `IPaymentProvider` interface.
  - Add real Kaspi QR integration (live credentials, webhook signature verification).
  - Support cash payment: order marked Paid by a worker after handoff.

- [ ] **Integration & end-to-end tests**
  - Use `WebApplicationFactory` with a real test PostgreSQL database (Docker).
  - Cover critical paths: place order → accept → complete → payment.
  - Run in GitHub Actions on every pull request.

- [ ] **FCM / APNs push infrastructure**
  - Replace local Capacitor notifications with server-side Firebase Cloud Messaging.
  - Backend sends push directly when order status changes — works even when app is closed.
  - Store device tokens per customer. Opt-out respected server-side.

- [ ] **Multi-language backend support**
  - Accept `Accept-Language` header on all endpoints.
  - Return error messages and status labels in the customer's preferred language.

---

## 💰 Monetization

### 1. SaaS Subscription (primary)

Charge café businesses a monthly or annual fee to use the platform.

| Tier           | Price           | Limits                                                                 |
| -------------- | --------------- | ---------------------------------------------------------------------- |
| **Starter**    | Free            | 1 location, 100 orders/month, basic analytics                          |
| **Growth**     | ₸25,000 / month | 3 locations, unlimited orders, full analytics, promo codes             |
| **Business**   | ₸60,000 / month | Unlimited locations, staff accounts, loyalty program, priority support |
| **Enterprise** | Custom          | White-label, custom domain, SLA, dedicated onboarding                  |

Implementation: add a `Plan` field to the tenant/location entity; gate features by plan in middleware.

### 2. Transaction Fee

Charge a small percentage of each order processed through the platform's payment flow.

- **0.5–1%** of every paid order (Kaspi QR, card).
- Only applies when the platform processes the payment — cash orders are exempt.
- Low friction: automatically deducted from monthly payout to the café.

### 3. Loyalty & Wallet Module (add-on)

- The in-app wallet top-up and loyalty point system are Premium features.
- Cafés on the Starter plan see the feature locked with an upgrade prompt.
- Alternatively: charge per top-up transaction (e.g. 1% platform fee on wallet loads).

### 4. Promoted / Featured Items

- Cafés can pay to feature up to 3 items at the top of the menu with a "Featured" badge.
- Flat monthly fee per featured slot: ₸5,000 / item / month.
- Admin UI to manage featured slots per location.

### 5. Setup & Onboarding Fee

- One-time fee of ₸30,000–₸80,000 for assisted onboarding: menu migration, staff training, custom branding, and first-month support.
- Reduces churn by ensuring the café actually launches successfully.

### 6. White-Label Licensing

- Sell the entire platform to a café chain or POS reseller as a white-labeled product.
- They pay a one-time licensing fee (₸500,000+) or a recurring royalty.
- They handle their own support; you provide updates and hosting optionally.

### 7. SMS / Notification Credits

- Server-side push notifications (FCM) and SMS fallbacks are metered.
- Include a free monthly credit (e.g. 500 pushes); overage charged per 1,000 messages.
- Incentivises cafés to only notify for genuinely important events.

### 8. Data & Benchmarking Reports

- Aggregate anonymized order data across all tenants.
- Sell monthly industry reports to cafés: "How does your avg order value compare to similar cafés in Almaty?"
- Low effort once analytics are built; high perceived value for café owners.

### Recommended Launch Path

1. Launch with **free tier** to acquire the first 10–20 cafés and prove product-market fit.
2. Introduce **Growth plan** once cafés rely on the platform (switching cost is high).
3. Add **transaction fee** when real Kaspi integration goes live.
4. Layer in **featured items** and **add-ons** as the customer base grows.

### Payment process

- Payment will be applied via pos terminal
- payment options Kaspi Bank, Halyk Bank, Freedom Bank
- Worker send payment via pos terminal manually
- Receive payment accept order
- for the manuall entry of payment need Phone number of customer.
- in admin page, when customer send with what payment options he/she is gonna pay, it will be seen in order details.
- customer when added the items, and go to cart, when go to place order, next step it must be choose payment options (kaspi bank, halyk bank or freedom bank)
- the admin page order details must also shown the phone number of customer
- on admin page, in order details, payment method cannot be changed, no dropdown, but worker can change is paid, or refunded. by default is unpaid.
- mark it as accepted only available if payment is paid.

### Integration with iikoCard platform (customer balance)

- api which will get the customer balance via phone number.
- payment add new option card balance
