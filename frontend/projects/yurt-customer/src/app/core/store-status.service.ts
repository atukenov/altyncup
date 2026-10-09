import { Injectable, computed, inject, signal } from '@angular/core';
import { LocationService } from './location.service';
import { buildScheduleInfo } from './schedule';

@Injectable({ providedIn: 'root' })
export class StoreStatusService {
  private location = inject(LocationService);

  private readonly now = signal(new Date());
  private noticeTimer?: ReturnType<typeof setTimeout>;

  /** True while the "we're closed" popup is on screen. */
  readonly noticeVisible = signal(false);

  readonly schedule = computed(() => buildScheduleInfo(this.location.workingHours(), this.now()));
  readonly isOpen = computed(() => this.schedule().isOpen);
  readonly nextOpenAt = computed(() => this.schedule().nextOpenAt);

  constructor() {
    setInterval(() => this.now.set(new Date()), 30_000);
  }

  /** Returns true when ordering is allowed; otherwise shows the closed popup and returns false. */
  guardOpen(): boolean {
    this.now.set(new Date());
    if (this.isOpen()) return true;
    this.showNotice();
    return false;
  }

  showNotice(): void {
    this.noticeVisible.set(true);
    clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.dismissNotice(), 4500);
  }

  dismissNotice(): void {
    clearTimeout(this.noticeTimer);
    this.noticeVisible.set(false);
  }
}
