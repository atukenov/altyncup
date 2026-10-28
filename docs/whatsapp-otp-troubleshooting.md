# WhatsApp OTP troubleshooting (Green API)

Registration, forgot-PIN and phone-change codes are sent through Green API `sendMessage`.
A `200` from Green API only means the message was **queued**. It is still dropped when the
instance is logged out or the number has no WhatsApp, so "no error, but no message" is normal
for those cases.

## Settings (env vars)

| Variable | Value |
|---|---|
| `Otp__Enabled` | `true` (when `false` nothing is sent; the code comes back as `devCode`) |
| `Otp__GreenApiBaseUrl` | The instance's **apiUrl** from the Green API console, e.g. `https://7103.api.greenapi.com`. The old shared `https://api.green-api.com` does not work for newer instances. |
| `Otp__IdInstance` | idInstance from the console |
| `Otp__ApiTokenInstance` | apiTokenInstance from the console |
| `Otp__WebhookToken` | A long random secret for the Green API webhook (see below). Empty = webhook turned off. |

## Check it

**Without the backend:**

```bash
export GREEN_API_URL=https://7103.api.greenapi.com
export GREEN_API_ID_INSTANCE=7103123456
export GREEN_API_TOKEN=...
backend/scripts/greenapi-otp-test.sh +77011234567
```

**Against a running backend** (super-admin JWT), using the deployed config:

```bash
curl -X POST https://<api-host>/api/v1/admin/otp/test \
  -H "Authorization: Bearer <super-admin token>" -H "Content-Type: application/json" \
  -d '{"mobileNumber":"+77011234567","sendTestMessage":true}'
```

The response lists config warnings and each Green API call (`getStateInstance`, `checkWhatsapp`,
`sendMessage`, `getMessage`) with its HTTP status, raw body and a hint.

## Common causes

- `stateInstance` is `notAuthorized`: scan the QR code in the console again.
- `existsWhatsapp: false`: the number has no WhatsApp.
- HTTP `466`: the free Developer plan only allows messaging a few numbers.
- HTTP `401/403/404`: wrong token/instance id, or wrong `GreenApiBaseUrl` host.
- `statusMessage: pending` that never changes: the instance phone is offline or logged out.

Every successful send is logged as `Green API queued OTP for {ChatId} as message {IdMessage}`;
failures are logged with the status, response body and a hint.

## Delivery status and instant alerts (Green API webhook)

Green API can call the backend back when WhatsApp reports what happened to a message, and when
the instance logs out or comes back. The backend uses one endpoint for both:

```
POST https://altyncup-production.up.railway.app/api/v1/webhooks/green-api
```

- `outgoingMessageStatus` updates the **Delivery** column in **WhatsApp codes → Requested codes**:
  Queued → Sent → Delivered → Read, or Failed / No WhatsApp. A failed delivery is also recorded as a
  "Not delivered" event, so it shows up in "Didn't get through".
- `stateInstanceChanged` sends the Telegram alert right away (for example "instance state is
  'notAuthorized'"), instead of waiting for the 10-minute check. The check still runs as a fallback
  and doesn't repeat an alert the webhook already sent.

Setup:

1. Generate a secret, e.g. `openssl rand -hex 32`, and set it on Railway as `Otp__WebhookToken`.
   Until it is set, the endpoint answers `401` to everything.
2. In the Green API console, open the instance → **Settings** (or **Notifications**) and set:
   - **Webhook URL** (`webhookUrl`): the URL above;
   - **Webhook URL token** (`webhookUrlToken`): the same secret. Green API sends it in the
     `Authorization` header;
   - **Receive statuses of outgoing messages** (`outgoingWebhook`): on;
   - **Receive instance state changes** (`stateWebhook`): on.

   Or do the same through the API:

   ```bash
   curl -X POST "$GREEN_API_URL/waInstance$GREEN_API_ID_INSTANCE/setSettings/$GREEN_API_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"webhookUrl":"https://altyncup-production.up.railway.app/api/v1/webhooks/green-api",
          "webhookUrlToken":"<Otp__WebhookToken>",
          "outgoingWebhook":"yes","stateWebhook":"yes"}'
   ```

   Settings can take a few minutes to apply.
3. Check: resend a code from the admin page. Within seconds its **Delivery** badge should change
   from Queued to Sent or Delivered. If it stays Queued, look in the server log for
   `Rejected Green API webhook` (wrong or missing token).

If the webhook can't send the token in a header (some proxies strip it), you can append
`?token=<secret>` to the webhook URL instead.

Webhooks from a different `idInstance` are ignored, and statuses never go backwards (a late
`sent` won't overwrite `delivered`).

## Alerts (Telegram)

The backend can message a Telegram chat when OTP delivery breaks:

- a code could not be sent (Green API error), at most once per 30 minutes;
- the Green API instance stops being `authorized`, and again when it recovers: instantly through the
  webhook above, otherwise within 10 minutes;
- 3+ codes were sent in the last hour and none were verified (messages probably queued but not delivered).

Setup:

1. In Telegram, message **@BotFather**, send `/newbot`, and copy the bot token.
2. Send any message to your new bot (or add it to a group), then open
   `https://api.telegram.org/bot<token>/getUpdates` and copy `chat.id`.
3. Set on Railway: `Alerts__TelegramBotToken=<token>` and `Alerts__TelegramChatId=<chat id>`.
4. Admin panel → **WhatsApp codes** → **Send test alert**.

Without these variables alerts are only written to the server log.

## Audit: who tried and failed

Every code is recorded in the `OtpEvents` table (sent, send failed, not delivered, verified,
wrong code, expired, too many attempts). The admin panel page **WhatsApp codes** (super admin) shows the
WhatsApp connection status, counts, a "didn't get through" list of numbers whose last code was
never confirmed, and the full history. The same data is at
`GET /api/v1/admin/otp/summary?hours=24` and `GET /api/v1/admin/otp/events`.
