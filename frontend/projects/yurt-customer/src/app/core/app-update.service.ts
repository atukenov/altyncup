import { Injectable, inject, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { App } from '@capacitor/app';
import { YurtApiService } from 'shared-api';

function compareVersions(a: string, b: string): number {
  const pa = a.split('.').map((n) => parseInt(n, 10) || 0);
  const pb = b.split('.').map((n) => parseInt(n, 10) || 0);
  const len = Math.max(pa.length, pb.length);
  for (let i = 0; i < len; i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

/**
 * Blocks the app behind an "update required" gate when the running native build
 * is older than the minimum version the backend reports for its platform.
 * A blank minimum version (the default) leaves the gate disabled, and any
 * failure to determine the local or required version fails open — this must
 * never trap a user who simply has no network connection.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly api = inject(YurtApiService);

  readonly updateRequired = signal(false);
  readonly storeUrl = signal('');

  async check(): Promise<void> {
    if (!Capacitor.isNativePlatform()) return;

    let currentVersion: string;
    try {
      currentVersion = (await App.getInfo()).version;
    } catch {
      return;
    }

    const platform = Capacitor.getPlatform();
    this.api.getAppUpdateInfo().subscribe({
      next: (info) => {
        const minVersion = platform === 'ios' ? info.minVersionIos : info.minVersionAndroid;
        const storeUrl = platform === 'ios' ? info.storeUrlIos : info.storeUrlAndroid;
        if (!minVersion || !storeUrl) return;

        if (compareVersions(currentVersion, minVersion) < 0) {
          this.storeUrl.set(storeUrl);
          this.updateRequired.set(true);
        }
      },
      error: () => {},
    });
  }
}
