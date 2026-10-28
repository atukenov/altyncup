import { Injectable, signal } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { Capacitor, PluginListenerHandle } from '@capacitor/core';
import { FirebaseAuthentication } from '@capacitor-firebase/authentication';
import { RegistrationStartResponse } from 'shared-models';

/** What the verify endpoints accept as proof of the phone number. */
export interface OtpProof {
  code?: string;
  firebaseIdToken?: string;
}

/**
 * Firebase Phone Auth is only wired up in the native apps, and only when the native plugin
 * is actually in the build (e.g. the iOS pod is installed). Otherwise the app keeps the
 * WhatsApp code: it never advertises Firebase, so the backend never asks it to.
 */
export const supportsFirebaseOtp = (): boolean =>
  Capacitor.isNativePlatform() && Capacitor.isPluginAvailable('FirebaseAuthentication');

/**
 * Tells the backend this build can send the code itself through Firebase. The backend only
 * switches a request to Firebase when Otp__Provider is Firebase *and* this header is present,
 * so older app builds keep getting WhatsApp codes.
 */
export const otpProvidersInterceptor: HttpInterceptorFn = (req, next) =>
  supportsFirebaseOtp() && /\/auth\/.*\/start$/.test(req.url)
    ? next(req.clone({ setHeaders: { 'X-Otp-Providers': 'firebase' } }))
    : next(req);

/**
 * One phone-verification attempt on one screen. After a "start" call, {@link begin} picks up
 * the channel from the response: for WhatsApp the backend already sent a 4-digit code; for
 * Firebase this sends the 6-digit SMS. {@link proof} then turns the typed code into what the
 * verify endpoint needs.
 */
export class OtpSession {
  /** How many code boxes to show. */
  readonly codeLength = signal(4);
  private provider: 'whatsapp' | 'firebase' = 'whatsapp';
  private verificationId: string | null = null;

  get isFirebase(): boolean {
    return this.provider === 'firebase';
  }

  /**
   * Resolves once the code is on its way. For Firebase on Android the number can be verified
   * instantly without a code; the ID token is returned then, so the screen can finish at once.
   */
  async begin(res: RegistrationStartResponse, resend = false): Promise<OtpProof | null> {
    this.provider = res.provider === 'firebase' ? 'firebase' : 'whatsapp';
    this.codeLength.set(res.codeLength ?? 4);
    if (!this.isFirebase) return null;

    const sent = await sendFirebaseSms(res.mobileNumber, resend);
    if ('idToken' in sent) return { firebaseIdToken: sent.idToken };
    this.verificationId = sent.verificationId;
    return null;
  }

  isComplete(code: string): boolean {
    return code.length === this.codeLength();
  }

  async proof(code: string): Promise<OtpProof> {
    if (!this.isFirebase) return { code };
    if (!this.verificationId) throw new Error('Request a new code.');
    await FirebaseAuthentication.confirmVerificationCode({
      verificationId: this.verificationId,
      verificationCode: code,
    });
    return { firebaseIdToken: await takeIdToken() };
  }
}

@Injectable({ providedIn: 'root' })
export class PhoneOtpService {
  session(): OtpSession {
    return new OtpSession();
  }
}

/** Readable message for a Firebase Phone Auth failure (wrong code, too many requests…). */
export function firebaseOtpError(err: unknown, fallback: string): string {
  const message = (err as { message?: string; code?: string })?.message ?? '';
  if (/invalid-verification-code|invalid verification code|sms code.*invalid/i.test(message)) return 'Incorrect code.';
  if (/code-expired|session-expired|expired/i.test(message)) return 'The code has expired. Request a new one.';
  if (/too-many-requests|quota|blocked all requests/i.test(message)) return 'Too many attempts. Try again later.';
  if (/invalid-phone-number/i.test(message)) return 'This phone number is not valid.';
  return fallback;
}

type SmsSent = { verificationId: string } | { idToken: string };

/** Ask Firebase to send the SMS and wait for "code sent", "verified instantly" or "failed". */
async function sendFirebaseSms(phoneNumber: string, resend: boolean): Promise<SmsSent> {
  const handles: PluginListenerHandle[] = [];
  const cleanup = () => handles.forEach((h) => void h.remove());

  try {
    return await new Promise<SmsSent>((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Could not send the SMS. Try again.')), 90_000);
      const done = (fn: () => void) => {
        clearTimeout(timer);
        fn();
      };

      Promise.all([
        FirebaseAuthentication.addListener('phoneCodeSent', (e) =>
          done(() => resolve({ verificationId: e.verificationId })),
        ),
        FirebaseAuthentication.addListener('phoneVerificationCompleted', () =>
          takeIdToken().then(
            (idToken) => done(() => resolve({ idToken })),
            (err) => done(() => reject(err)),
          ),
        ),
        FirebaseAuthentication.addListener('phoneVerificationFailed', (e) =>
          done(() => reject(new Error(e.message))),
        ),
      ])
        .then((hs) => {
          handles.push(...hs);
          return FirebaseAuthentication.signInWithPhoneNumber({
            phoneNumber,
            resendCode: resend && Capacitor.getPlatform() === 'android',
          });
        })
        .catch((err) => done(() => reject(err)));
    });
  } finally {
    cleanup();
  }
}

/**
 * Read the Firebase ID token for the backend, then sign out of Firebase again: the app's own
 * session is the backend's JWT, Firebase is only used to prove the number.
 */
async function takeIdToken(): Promise<string> {
  const { token } = await FirebaseAuthentication.getIdToken({ forceRefresh: false });
  await FirebaseAuthentication.signOut().catch(() => undefined);
  return token;
}
