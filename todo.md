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

---

## 🔲 Upcoming Features

### Admin Panel

- [x] **Dashboard with live metrics**
  - Real-time cards: orders today, revenue today, average order value, pending orders count.
  - Simple bar chart of orders by hour for the current day.
  - Quick-action buttons: view all pending, go to menu.

- [ ] **Order management improvements**
  - Bulk-accept / bulk-decline multiple orders at once with a shared ETA or reason.
  - Filter orders by status, location, date range, and payment method.
  - Search orders by order number or customer phone number.
  - Print-friendly order ticket view (for kitchen display or receipt printer).

- [x] **Worker accounts & role-based access**
  - Workers can only see and manage orders — no access to menu, promotions, or analytics.
  - Admins can create, deactivate, and reset passwords for worker accounts.
  - Audit log: track which worker accepted/declined each order.

- [ ] **Menu management enhancements**
  - Drag-and-drop reordering for categories and items.
  - Bulk availability toggle: mark an entire category as unavailable (e.g. "Sold out today").
  - Item image upload directly from the admin panel instead of pasting a URL.
  - Duplicate item: copy an existing item as a starting point for a new one.

- [ ] **Promotions & discount codes**
  - Create time-limited or usage-limited discount codes (percentage or fixed amount).
  - Assign promotions to specific categories, items, or all orders.
  - View usage stats per promotion: how many times used, total discount given.

- [ ] **Analytics page**
  - Top-selling items (last 7 / 30 days) with quantity and revenue.
  - Revenue over time (daily/weekly/monthly) chart.
  - Peak hours heatmap.
  - Most popular topping combinations.
  - Export to CSV.

- [x] **Customer list**
  - View all registered customers with registration date, order count, and total spend.
  - Search by phone number.
  - View a customer's full order history from the admin side.

- [ ] **Location management**
  - Edit working hours, contact phone, and active status from the admin panel.
  - Temporarily close a location (orders blocked while inactive).

---

### Customer Mobile App

- [x] **Home screen improvements**
  - Personalized greeting with customer's first name.
  - "Order again" shortcut: re-add the last completed order to cart in one tap.
  - Highlight promoted or new items with a badge ("New", "Popular").

- [x] **Cart & checkout enhancements**
  - Cart persists across app sessions (saved locally) so items aren't lost on refresh.
  - Item quantity adjustment (+ / −) directly in the cart without reopening the item.
  - Special instructions text field per item (e.g. "extra hot", "no lid").
  - Estimated pick-up time shown at checkout based on current queue.

- [ ] **Favorites**
  - Heart icon on each menu item to save it to a Favorites list.
  - Dedicated Favorites tab or section on the home screen for quick reorder.
  - By saving also saves the pre-chosen toppings.

- [x] **Push notifications**
  - Notify customer when their order status changes (Accepted, Ready, Declined).
  - Opt-in/opt-out from the profile screen.
  - Deep-link the notification directly to the order detail page.

- [ ] **Order rating & feedback**
  - After an order is marked Completed, prompt the customer to rate it (1–5 stars).
  - Optional short text comment.
  - Ratings visible to admins in the order detail and analytics.

- [ ] **Loyalty points**
  - Earn 1 point per ₸10 spent.
  - Points balance shown on the profile screen.
  - Redeem points for a discount at checkout (e.g. 100 points = ₸100 off).

- [x] **Profile & account settings**
  - Edit display name.
  - Change PIN with current-PIN verification.
  - View total orders placed and total spent (lifetime stats).
  - Delete account (GDPR-style data removal).

- [ ] **Order scheduling**
  - Choose a pick-up time slot up to 2 hours in advance.
  - Order enters the queue only when the prep window approaches.

---

### Backend & Infrastructure

- [ ] **Refresh token rotation**
  - Issue short-lived access tokens (15 min) and long-lived refresh tokens (7 days).
  - Rotate refresh token on each use; invalidate old token immediately.
  - Endpoint: `POST /api/auth/refresh`.

- [x] **Rate limiting**
  - Per-IP and per-user rate limits on auth endpoints to prevent brute-force PIN attacks.
  - Configurable via `appsettings.json`.

- [ ] **Background job queue**
  - Use Hangfire or a lightweight in-process queue for async work: sending push notifications, expiring promotions, generating daily analytics snapshots.

- [ ] **Structured logging & error tracking**
  - Enrich Serilog with request ID, user ID, and order ID context.
  - Ship logs to a sink (Seq, Grafana Loki, or similar) for searchable centralized logging.
  - Capture unhandled exceptions to an error-tracking service (Sentry or similar).

- [ ] **Database migrations safety**
  - Add a startup check that warns (does not block) if unapplied migrations exist when running in Production.
  - Separate migration runner from the web app so migrations are applied by a dedicated init container in Docker.

- [ ] **Health check endpoint**
  - `GET /health` returns DB connectivity, SignalR hub status, and current queue depth.
  - Integrated with Docker `HEALTHCHECK` directive.

- [ ] **API versioning**
  - Version the public API (`/api/v1/...`) so the mobile app and backend can be updated independently without breaking older app versions still in use.

- [ ] **Payment provider expansion**
  - Abstract the payment service behind an `IPaymentProvider` interface.
  - Add a real Kaspi QR integration (live credentials, webhook signature verification).
  - Support cash payment flow: order is marked Paid by a worker after handoff.

- [x] **Order archival**
  - Orders older than 90 days are moved to a cold `ArchivedOrders` table to keep the main table fast.
  - Archive job runs nightly via the background queue.

- [ ] **Integration & end-to-end tests**
  - Use `WebApplicationFactory` with a real test PostgreSQL database (Docker container spun up in CI).
  - Cover the critical paths: place order → accept → complete → payment.
  - Run in GitHub Actions on every pull request.
