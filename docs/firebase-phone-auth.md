# Phone verification with Firebase SMS

Customers prove their phone number with a one-time code during registration, "Forgot PIN",
changing their number, and verifying an old account's number. The code can come from two places:

| `Otp__Provider` | Who sends the code | Code length |
|---|---|---|
| `WhatsApp` (default) | The backend, through Green API WhatsApp | 4 digits |
| `Firebase` | The customer app, through Firebase Phone Auth (SMS) | 6 digits |

The code screens read the length from the backend, so they show 4 or 6 boxes automatically.

## How the Firebase flow works

1. The app calls a `.../start` endpoint with the header `X-Otp-Providers: firebase`.
   Only the iOS and Android apps send this header; the web app doesn't.
2. If `Otp__Provider=Firebase` and the header is present, the backend sends nothing. It saves
   the pending request (name, PIN, etc.) and answers `provider: "firebase", codeLength: 6`.
   Everyone else (web, older app builds) gets `provider: "whatsapp", codeLength: 4` and a
   WhatsApp code, exactly as before.
3. The app asks Firebase to send the SMS (`@capacitor-firebase/authentication`), the customer
   types the 6-digit code, and Firebase returns an ID token. On Android, Firebase can sometimes
   verify the number automatically without a code.
4. The app sends `firebaseIdToken` (instead of `code`) to the `.../verify` endpoint. The backend
   checks the token with the Firebase Admin SDK (signature, expiry, project) and requires its
   `phone_number` to be the same number as the pending request.

Switching back to `Otp__Provider=WhatsApp` is always safe: new requests go back to WhatsApp codes.

## Settings (Railway env vars)

| Variable | Value |
|---|---|
| `Otp__Provider` | `Firebase` to turn it on, `WhatsApp` to turn it off |
| `Firebase__ServiceAccountJson` | Service account JSON for project `altyncup-kz` (already used for push) |
| `Firebase__ProjectId` | `altyncup-kz` |

Keep the Green API variables set: the web app and older app builds still use WhatsApp.

## Firebase console checklist

- Blaze plan (SMS is not available on the free plan).
- Authentication → Sign-in method → Phone: enabled.
- Authentication → Settings → SMS region policy: allow Kazakhstan (+7).
- Android app `com.yurt.app`: SHA-1 and SHA-256 fingerprints added (debug, release and the
  Play App Signing key), Play Integrity API enabled, and `google-services.json` placed in
  `frontend/android/app/`.
- iOS app `com.altyncup.app`: APNs auth key uploaded (Project settings → Cloud Messaging).
  The reCAPTCHA URL scheme (Encoded App ID) is already in `Info.plist`.
- Optional, for testing without real SMS: Authentication → Sign-in method → Phone → test numbers.

After pulling this change run `npm install` and `npx cap sync` in `frontend/`, then rebuild
the iOS and Android apps.

## Audit

The admin page "WhatsApp codes" shows Firebase requests with a "Firebase SMS" badge and no
code (Firebase sends it, so we never see it, and "Resend" is hidden). In the history,
`CodeSent` with detail "Firebase SMS (sent by the app)" means the app was told to send the SMS,
`Verified` with detail "Firebase SMS" means the customer finished, and `WrongCode` with a
Firebase reason means the token was rejected (wrong number, expired, not configured).
Wrong codes typed into the app are rejected by Firebase itself and are not recorded.
