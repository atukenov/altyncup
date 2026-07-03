# TODO — Altyncup Product Roadmap

---

## 🔲 Upcoming

### Bug Fixes & Polish

- [ ] **Order search**
  - Search live orders by order number (last 6 characters) or customer phone number.
  - Useful when a customer calls to ask about their order status.

- [x] **Admin orders — location memory**
  - Save selected location to localStorage; pre-select it on next visit.
  - Show address in the location dropdown to differentiate branches with similar names.

- [x] **Remove standalone Dashboard page**
  - Redundant now that analytics and live orders are mature.
  - Remove from sidebar navigation; `/dashboard` now redirects to `/orders`.

- [ ] **Menu item sort order**
  - Display items in a configurable order rather than insertion order.
  - Drag-and-drop reordering in the admin menu list.

- [ ] **Menu item image upload (ignore)**
  - Upload images directly from the admin panel instead of pasting a URL.

---

### Admin Panel

- [ ] **Inventory and stock tracking (ignored)**
  - Track stock levels per ingredient or finished item (e.g. "Oat milk: 3 packs").
  - Low-stock threshold per item: admin receives a notification when breached.
  - "Mark sold out" toggle on any menu item instantly hides it from customers until restocked.
  - Optional: auto-decrement stock when an order is accepted.

- [ ] **Menu scheduling (time-based availability)**
  - Each item or category can be restricted to a time window (e.g. Breakfast menu 07:00–11:00).
  - Items outside their scheduled window are hidden from customers automatically — no manual toggling.

- [ ] **Thermal receipt printer integration**
  - Connect a Bluetooth or Wi-Fi ESC/POS thermal printer.
  - Auto-print a kitchen ticket when a worker accepts an order (order number, items, toppings, variants, notes, customer name).

- [ ] **Location performance metrics in analytic panel**
  - Per-location stats: orders accepted/declined/completed, average time to accept.
  - Per-location stats: orders accepted, avg acceptance time, declined count, busiest hours.
  - Helps identify slow responders during peak hours.

- [ ] **Scheduled auto-reports via email**
  - Auto-email the owner a monthly summary every first day of the month: orders last month, revenue, top items, month-over-month comparison.
  - Configurable to daily or monthly cadence (ignore).

- [ ] **Telegram bot for admin alerts (ignore)**
  - Send critical events to a configured Telegram group: new orders, low stock alerts, payment failures.
  - Workers can reply to an alert to accept or decline an order directly from Telegram.
  - Very high adoption in KZ — most café teams already coordinate via Telegram.

- [ ] **Iika loyalty integration**
  - See customers balance in customer panel.

---

### Customer App

- [ ] **Iika loyalty integration**
  - Show the customer's Iika balance on the Profile and cart screens (fetched by phone number via Iika API).
  - After a completed order, automatically credit a configurable percentage of the order total as Iika points.
  - Customers can apply their Iika balance as full or partial payment at checkout.
  - Order history shows how many Iika points were earned per order.

- [ ] **Scheduled / pre-orders (ignore)**
  - Customer picks a pickup time when placing an order (e.g. "Ready at 09:00").
  - The KDS receives the order at the right time so it is fresh on pickup.
  - Customer receives a reminder push notification 10 minutes before the scheduled time.

- [ ] **Gamification and loyalty points via Iika (just a game)**
  - Unlockable achievements: "First order", "10 orders", "Tried every category", etc.
  - Naming of gained points in Iika: "Beginner", "Pro", "Game killer" and etc.

- [ ] **iOS home screen widget (ignore)**
  - A small widget showing the active order status or a "Reorder last" quick action.
  - Built with WidgetKit; refreshes via background fetch when order status changes.

- [ ] **App update gate (backend-controlled)**
  - Customer receive pop up to update app, with a link to app store.
  - the pop up modal is showing only if app version is lower than current version.

---
