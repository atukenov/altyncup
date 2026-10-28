import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { CommonModule } from '@angular/common';
import { AuthStateService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { AdminLangService } from '../core/lang.service';
import { AdminTranslatePipe } from '../core/translate.pipe';

const SIDEBAR_KEY = 'yurt_admin_sidebar_collapsed';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, AdminTranslatePipe],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.css',
})
export class AdminShellComponent {
  readonly auth = inject(AuthStateService);
  readonly langService = inject(AdminLangService);
  private router = inject(Router);
  private toast = inject(ToastService);

  collapsed = signal(localStorage.getItem(SIDEBAR_KEY) === '1');
  mobileNavOpen = signal(false);

  private mobileQuery = window.matchMedia('(max-width: 767.98px)');
  private isMobile = signal(this.mobileQuery.matches);

  // The mobile drawer always shows full labels; the icon-only rail is desktop-only.
  compact = computed(() => this.collapsed() && !this.isMobile());

  constructor() {
    const onChange = (e: MediaQueryListEvent) => {
      this.isMobile.set(e.matches);
      if (!e.matches) this.mobileNavOpen.set(false);
    };
    this.mobileQuery.addEventListener('change', onChange);
    inject(DestroyRef).onDestroy(() => this.mobileQuery.removeEventListener('change', onChange));

    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.mobileNavOpen.set(false));
  }

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem(SIDEBAR_KEY, next ? '1' : '0');
  }

  initials(): string {
    const name = this.auth.currentUser?.displayName ?? '?';
    return name.slice(0, 2).toUpperCase();
  }

  logout(): void {
    this.auth.logout();
    this.toast.info('Signed out');
    this.router.navigate(['/auth/login']);
  }
}