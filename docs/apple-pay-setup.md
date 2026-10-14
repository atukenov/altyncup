# Apple Pay via Freedom Pay — manual setup (issue #49)

The code (backend `FreedomPayPaymentProvider`, checkout wiring, native `ApplePayPlugin.swift`
scaffold) is in place on branch `claude/apple-pay-freedom-pay`, but it ships **disabled by
default** (`FreedomPay:Enabled = false`) and the native Swift plugin's SDK calls are
commented-out TODOs — three things need to happen outside this repo before it can go live.

## 1. Apple Developer portal

1. [developer.apple.com/account](https://developer.apple.com/account) → Certificates,
   Identifiers & Profiles → Identifiers → register a **Merchant ID**
   (e.g. `merchant.com.yurt.app`).
2. On the app's own Identifier (`com.yurt.app`), enable the **Apple Pay** capability and
   link it to that Merchant ID.
3. Create an **Apple Pay Payment Processing Certificate**: Certificates → + → Apple Pay
   Payment Processing Certificate. This needs a CSR — generate one from a Mac via
   Keychain Access → Certificate Assistant → Request a Certificate from a Certificate
   Authority. Upload the resulting cert back to the portal.
4. In Xcode (`frontend/ios/App/App.xcworkspace`), target `App` → Signing & Capabilities →
   **+ Capability** → Apple Pay → select the Merchant ID from step 1. (This writes the
   entitlement and registers the capability with the portal — same reason the push
   notification entitlement needed to go through this UI, not a hand-edited file.)

## 2. Freedom Pay merchant setup

Freedom Pay's own Apple Pay iOS SDK docs: https://docs.freedompay.kz/doc-690101 (Kazakhstan;
a near-identical Kyrgyzstan mirror exists at https://docs.freedompay.kg/doc-833034, whose SSL
cert was expired as of this writing — use the `.kz` one). Confirmed via search to cover
enabling the Apple Pay capability, checking device availability, setting supported networks,
and "initializing and confirming payment through the SDK" — consistent with this scaffold's
`createApplePayment`/`confirmApplePayment` shape — but the page itself returned 404 when
fetched programmatically, so read it directly in a browser rather than trusting this repo's
guess at exact method signatures.

Don't confuse this with the *other*, unrelated company also named "FreedomPay" (a US
enterprise payments provider) that turns up in search results — different company, different
SDK, not what issue #49's `freedompay-global/merchant-sdk-ios` refers to.

1. Apply for / confirm a Freedom Pay merchant account that supports **Apple Pay**
   specifically — this is a separate capability from normal card processing, confirm it's
   enabled for your merchant ID with their support team.
2. Get the merchant ID, secret key, and confirm which `Region` value to use (issue #49
   assumes `.kz`).
3. **Ask whether your Apple Merchant ID or Apple Pay certificate needs to be registered in
   Freedom Pay's own merchant dashboard.** Not confirmed for Freedom Pay specifically, but
   it's the common pattern for this kind of integration in this market — Halyk's epayment.kz
   requires uploading both a Payment Processing Certificate and a Merchant Identity
   Certificate to the merchant profile before Apple Pay shows as active
   (https://epayment.kz/en-US/docs/Setting%20up%20Apple%20Pay%20in%20your%20merchant%20profile).
   Don't assume Freedom Pay skips this step without checking.
4. **Before writing the real SDK calls**, verify the exact method/type names in
   `ApplePayPlugin.swift`'s TODO comments (`StandardPaymentRequest`,
   `createApplePayment`, `confirmApplePayment`) against Freedom Pay's current SDK docs —
   these were taken from a description, not a verified live integration (see issue #49's
   "Open questions"). Ask Freedom Pay support directly if their docs are unclear on the
   Apple Pay flow specifically.

## 3. Add the Freedom Pay SDK to Xcode

1. Open `frontend/ios/App/App.xcworkspace` in Xcode.
2. File → Add Package Dependencies → paste
   `https://github.com/freedompay-global/merchant-sdk-ios.git`.
3. Add it to the `App` target.
4. In `frontend/ios/App/App/ApplePayPlugin.swift`, uncomment `import FreedomPaymentSdk`
   and the TODO blocks, initialize `freedomApi` with the real merchant id/secret/region
   (as native config — e.g. an `.xcconfig` build setting or `Info.plist` entry — never
   fetched over the network or committed to git).

**Not done in this change** — same reasoning as the Android FCM setup in
`docs/push-notifications-setup.md`: adding the package reference to the committed Xcode
project before it's actually available would break every other dev's/CI's build.

## 4. Backend config

Once the Apple Merchant ID exists, set it and flip the feature on:

- Local: `dotnet user-secrets set "FreedomPay:AppleMerchantId" "merchant.com.yurt.app"`
  (run from `backend/src/Yurt.WebApi`), then set `FreedomPay:Enabled = true` directly in
  `appsettings.Development.json` or via `dotnet user-secrets set "FreedomPay:Enabled" "true"`.
- Production (Railway): `FreedomPay__Enabled=true`, `FreedomPay__AppleMerchantId=merchant.com.yurt.app`
  as environment variables (same double-underscore convention as `Firebase__ServiceAccountJson`).

Neither of these is a secret — Freedom Pay's own merchant ID/secret key live natively in
the iOS app (step 3), not in backend config, since the SDK calls happen client-side.

## 5. Testing

- Apple Pay requires a physical device — Simulator can present the sheet but can't
  complete a real authorization.
- Add a test card to Wallet on the test device, signed in with a **Sandbox Apple ID**
  (Settings → Developer → Sandbox Apple Account, or via a TestFlight build).
- Confirm with Freedom Pay whether their Apple Pay integration has a sandbox mode or is
  production-only (issue #3's research on the Halyk alternative found Apple Pay by API
  integrations are sometimes production-only with no sandbox — confirm which applies
  here before assuming test transactions are free/reversible).
