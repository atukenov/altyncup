import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthStateService } from 'shared-api';
import { ToastService } from 'shared-ui';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.css',
})
export class AdminShellComponent {
  readonly auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);

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