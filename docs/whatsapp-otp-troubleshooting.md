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

## Alerts (Telegram)

The backend can message a Telegram chat when OTP delivery breaks:

- a code could not be sent (Green API error), at most once per 30 minutes;
- the Green API instance stops being `authorized` (checked every 10 minutes), and again when it recovers;
- 3+ codes were sent in the last hour and none were verified (messages probably queued but not delivered).

Setup:

1. In Telegram, message **@BotFather**, send `/newbot`, and copy the bot token.
2. Send any message to your new bot (or add it to a group), then open
   `https://api.telegram.org/bot<token>/getUpdates` and copy `chat.id`.
3. Set on Railway: `Alerts__TelegramBotToken=<token>` and `Alerts__TelegramChatId=<chat id>`.
4. Admin panel → **WhatsApp codes** → **Send test alert**.

Without these variables alerts are only written to the server log.

## Audit: who tried and failed

Every code is recorded in the `OtpEvents` table (sent, send failed, verified, wrong code,
expired, too many attempts). The admin panel page **WhatsApp codes** (super admin) shows the
WhatsApp connection status, counts, a "didn't get through" list of numbers whose last code was
never confirmed, and the full history. The same data is at
`GET /api/v1/admin/otp/summary?hours=24` and `GET /api/v1/admin/otp/events`.
