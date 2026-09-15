# TODO — Altyncup Product Roadmap

_Last reviewed against the codebase: 2026-09-15._

---

## 🔲 Upcoming

### Bug Fixes & Polish

- [x] **Order search**
  - Shipped — `orders-live.component.ts` has a debounced search box wired to `getAdminOrders(status, locationId, search)`, matching by order number or phone.

- [x] **Admin orders — location memory**
  - Save selected location to localStorage; pre-select it on next visit.
  - Show address in the location dropdown to differentiate branches with similar names.

- [x] **Remove standalone Dashboard page**
  - Redundant now that analytics and live orders are mature.
  - Removed from sidebar navigation; `/dashboard` now redirects to `/orders`.

- [x] **Menu item sort order**
  - Shipped 2026-09-15 — `MenuItem` got a `SortOrder` column (migration `AddMenuItemSortOrder`); `MenuCategory.SortOrder` already existed but had no admin UI, so in practice every category sorted alphabetically.
  - Admin menu page now has drag-and-drop reordering (Angular CDK `cdkDrag`/`cdkDropList`, already a dependency) for both categories and items-within-a-category, plus keyboard-accessible ▲/▼ buttons. New `PATCH /admin/menu/categories/reorder` and `/admin/menu/items/reorder` endpoints.

- [ ] **Menu item image upload** _(ignore for now)_
  - Upload images directly from the admin panel instead of pasting a URL.
  - Confirmed still URL-paste only (`menu-management.component.html`) — no `<input type="file">`, no storage backend wired up.
  - Needs: a multipart upload endpoint + object storage (S3/Cloudinary/local disk), and an admin file-picker UI. No native camera/gallery Capacitor plugin is installed either, if mobile capture is ever wanted.

---

### Admin Panel

- [ ] **Inventory and stock tracking** _(ignored)_
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

- [x] **Location performance metrics in analytics panel** _(mostly shipped — verify one detail)_
  - `analytics.component.ts` already has a per-location filter and a location-performance bar chart alongside revenue/top-items/hourly breakdowns.
  - Double-check it surfaces the specific "avg time to accept" and "declined count" metrics from the original ask — if not, add those two series to the existing per-location view rather than building a new page.

- [ ] **Scheduled auto-reports via email** _(cadence config: ignore)_
  - Auto-email the owner a monthly summary every first day of the month: orders last month, revenue, top items, month-over-month comparison.
  - No email/SMTP service exists in the backend today — needs an `IEmailSender` (SMTP/SendGrid) plus a scheduled job (Hangfire or a hosted `BackgroundService`) for the monthly cadence.

- [ ] **Telegram bot for admin alerts** _(ignore)_
  - Send critical events to a configured Telegram group: new orders, low stock alerts, payment failures.
  - Workers can reply to an alert to accept or decline an order directly from Telegram.
  - Very high adoption in KZ — most café teams already coordinate via Telegram.
  - Worth reconsidering ahead of the real payment integration below — payment failures currently have no external alert channel at all.

- [x] **iiko loyalty integration**
  - Shipped: admin can view a customer's live iiko balance, manually refresh it, and self-heal an unlinked/stale wallet ID. Earn-credit retry + safety-net sweep for missed credits also shipped (2026-08-27).

- [x] **Order rating / feedback review**
  - Shipped 2026-09-15 — admin order detail panel (`orders-live.component`) now shows the customer's star rating and comment when present, sourced from the same `Order.Rating`/`RatingComment` fields the customer app writes.

- [ ] **NEW — Menu item classification tags**
  - Add structured tags to `MenuItem` (milk-based, iced, roast level, filter method, decaf, dessert/pastry) — today it only carries a free-text category name synced from iiko, which can't be pattern-matched reliably.
  - Unlocks 8 currently-blocked customer achievement badges (see Gamification below) and doubles as customer-facing filter facets ("show me iced drinks").

---

### Customer App

- [x] **iiko loyalty integration**
  - Shipped in full: balance shown on Profile and cart (fetched by phone), configurable earn percent on completed orders (currently 10%), full/partial balance redemption at checkout, order history shows points earned per order, plus retry + safety-net sweep for missed earn-credits.

- [ ] **Scheduled / pre-orders** _(ignore)_
  - Customer picks a pickup time when placing an order (e.g. "Ready at 09:00").
  - The KDS receives the order at the right time so it is fresh on pickup.
  - Customer receives a reminder push notification 10 minutes before the scheduled time.

- [~] **Gamification and loyalty points via iiko** — **18 of 28 badges shipped (2026-09-12)**
  - Live: First Drink, Coffee Beginner, Gold Cup, Constant, Star of the Altyncup, 100 Cups, Coffee Baron, Champion, Early Riser, Midnight Brew, Weekend Warrior, Streak Master, Caffeine Shield, Perfect Brew, Bean Collector, Bean Counter, Globetrotter — all driven by `GET /auth/me/stats` (see `frontend/projects/yurt-customer/public/ACHIEVEMENTS.md` for the full ledger).
  - Blocked on **menu item tagging** (8 badges): Double Shot, Flavor Adventurer, Milk Artist, Iced Coffee Ace, Roast Master, Pour-Over Pro, Decaf Diplomat, Sweet Tooth — see the admin task above.
  - Blocked on **new product surface**: Social Butterfly (needs a referral system, proposed below). Barista Fan's data dependency (order ratings) shipped 2026-09-15, but the achievement itself isn't wired up yet.
  - Remaining polish: the unlock toast currently only fires on the Achievements page itself (compared against `localStorage['yurt_seen_achievements']`); wiring it into the order-confirmation screen was deliberately deferred as a separate, more invasive change.

- [ ] **iOS home screen widget** _(ignore)_
  - A small widget showing the active order status or a "Reorder last" quick action.
  - Built with WidgetKit; refreshes via background fetch when order status changes.

- [x] **App update gate (backend-controlled)**
  - Shipped 2026-09-15 — new `AppUpdate` config section (`MinVersionIos`/`MinVersionAndroid`/store URLs, blank = disabled) served via `GET /app/update-info`. Customer app compares against the native build version (`@capacitor/app`, newly added) on startup and shows a non-dismissible modal with an "Update Now" link when below the configured minimum. Inert by default until an operator sets a minimum version.
  - Note: run `npx cap sync` before the next native build so Xcode/Android Studio pick up the new plugin — not run automatically in this session.

- [ ] **NEW — Real remote push notifications**
  - Current "push notifications" are `@capacitor/local-notifications` only — device-local scheduling with a web `Notification` fallback. There is no `@capacitor/push-notifications` plugin and no FCM/APNs wiring, so an order-status push will **not** reach a customer whose app is fully closed (only foregrounded/backgrounded).
  - Add Firebase Cloud Messaging (Android) + APNs (iOS) device-token registration on the backend, and send on order status change instead of relying on local scheduling.

- [ ] **NEW — Referral program**
  - Invite-a-friend flow: shareable code/link, bonus (e.g. iiko points) credited to both sides when the invitee completes their first order.
  - Unlocks the "Social Butterfly" achievement and reuses the share/link UX already built for group ordering.

- [x] **NEW — Order rating & feedback**
  - Shipped 2026-09-15 — 5-star rating + optional comment on the order-detail page once an order is `Completed` (`POST /orders/{id}/rating`, one rating per order). Feeds the admin "Order rating / feedback review" above.
  - Does not yet unlock the "Barista Fan" achievement — that wiring wasn't part of this pass.

- [ ] **NEW — Biometric login**
  - Face ID / Touch ID / fingerprint as a quick-unlock alternative to the 4-digit PIN.
  - No biometric plugin is currently installed; would need a Capacitor biometric-auth plugin plus a secure local credential store.

---

### Payments & Monetization Readiness

- [ ] **NEW — Real Kaspi Pay integration** — 🔴 **critical**
  - The only payment provider wired up today is `KaspiSandboxPaymentProvider`, a self-simulating mock that generates fake invoices, sleeps, and POSTs synthetic webhooks to itself (with knobs like `DuplicateWebhookChance`/`NetworkFailureChance` for testing).
  - **No real money is being processed by the platform today.** Replace it with a production Kaspi Pay/Kaspi QR (or chosen acquirer) integration before any live launch — this also unblocks the transaction-fee monetization path in `docs/monetization.md`.

- [ ] **NEW — Refund handling & reconciliation**
  - No refund endpoint or service exists anywhere in the Payments feature (only create/status/webhook flows).
  - Add an admin-initiated refund flow tied to the real provider, plus a reconciliation job to catch missed or duplicate webhook events.

- [ ] **NEW — Extend webhook idempotency to the production provider**
  - Idempotency is already tested for the sandbox path (`WebhookIdempotencyTests`); make sure the same guarantee holds once a real provider with its own webhook semantics is swapped in.

---

### Security & Reliability

- [x] **NEW — Close rate-limit gaps**
  - Shipped 2026-09-15 — added explicit per-IP rules for `admin/auth/login`, `admin/auth/refresh`, `admin/auth/logout`, `auth/refresh`, `auth/logout`, `payments/create`, and `loyalty/me` in `appsettings.json`'s `IpRateLimiting.GeneralRules`.

- [x] **NEW — Enforce HTTPS redirection in-app**
  - Shipped 2026-09-15 — `app.UseHttpsRedirection()` added in `Program.cs` after `UseForwardedHeaders`, so it's a no-op behind the Railway proxy (which already forwards `X-Forwarded-Proto`) and only kicks in if the proxy config ever changes.

- [x] **NEW — Add a Content-Security-Policy header**
  - Shipped 2026-09-15 — `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` added alongside the existing security headers, applied outside Development so it never interferes with the self-hosted Swagger UI.

- [x] **NEW — Stop committing dev credentials**
  - Shipped 2026-09-15 — the local Postgres connection string moved to `dotnet user-secrets` (`UserSecretsId` added to `Yurt.WebApi.csproj`); `appsettings.Development.json` now just points to user-secrets in a comment. (It was already gitignored and had never actually been committed with a real password — this was a hygiene fix, not a live leak.)

- [ ] **NEW — Optimistic concurrency on GroupCart**
  - Concurrent edits/joins to a shared group cart currently have no `RowVersion`/concurrency token — only implicit DB transaction ordering. Add one to prevent lost updates when multiple participants edit at the same moment.

- [~] **NEW — Close test-coverage gaps**
  - Partially shipped 2026-09-15 — added 54 unit tests (`Yurt.UnitTests`, 83 total now passing) covering Menu (incl. the new reorder logic), Locations, Favorites, Promotions, Reports, Workers, Customers pagination/search, and — prioritized per the note below — the `PaymentWebhookValidator` signature/timestamp checks and `KaspiSandboxPaymentProvider`.
  - Still open: rate-limiting behavior and admin controllers at the HTTP level need integration tests (`Yurt.IntegrationTests`, Docker/Testcontainers-based) rather than unit tests — not runnable in the sandbox this session was done in, so intentionally left alone rather than writing unverified tests. `DbSet.FindAsync`-based code paths (most single-entity update/delete methods) also remain untested since the project's mocking approach (`MockQueryable.NSubstitute`) can't emulate `FindAsync`; only list/create/`FirstOrDefaultAsync`-based paths could be covered this way. Push notifications also still untested.

---

### UX & Accessibility Polish

- [x] **NEW — Accessibility pass on the admin panel**
  - Shipped 2026-09-15 — icon-only buttons and toggle switches labeled, all modal dialogs got `role="dialog"`/`aria-modal`/`aria-labelledby`/Escape-to-close (including the shared confirm-dialog and toast components, so every admin delete confirmation and toast benefits), sidebar nav got `aria-current`/landmark labels, the login form's labels are now programmatically associated with their inputs, and the customer list (previously a `routerLink` on a plain `<tr>` with no keyboard path at all) is now keyboard-navigable. Also fixed one invalid nested-`<button>`-inside-`<button>` structure in the category sidebar.
  - Not done: full `label`/`for` association on every text-input form field across all admin dialogs — only the login form and a few standalone filter inputs got this; the broader retrofit was judged out of scope for a "low risk" pass given its size.

- [ ] **NEW — Global HTTP error interceptor (frontend)**
  - Today only 401/token-refresh is handled centrally (`auth.interceptor.ts`); every other error is handled ad hoc per component via a toast call. A shared interceptor would standardize messaging (offline vs. 5xx vs. validation) and centralize error logging.

- [ ] **NEW — Offline detection**
  - No global network-status service exists. Show a banner and disable checkout when offline instead of letting requests fail silently mid-order.

---
