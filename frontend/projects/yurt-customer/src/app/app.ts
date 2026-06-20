import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { StatusBar, Style } from '@capacitor/status-bar';
import {
  AuthStateService,
  NotificationService,
  OrderNotificationService,
  SignalrService,
  YurtApiService,
} from 'shared-api';
import { ToastContainerComponent } from 'shared-ui';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private signalr = inject(SignalrService);
  private orderNotifications = inject(OrderNotificationService);
  private notifications = inject(NotificationService);

  ngOnInit(): void {
    if (Capacitor.isNativePlatform()) {
      StatusBar.setOverlaysWebView({ overlay: true });
      StatusBar.setStyle({ style: Style.Light });
    }

    this.api.configure(environment.apiUrl);
    this.signalr.configure(environment.apiUrl);

    if (this.auth.isLoggedIn) {
      const rt = this.auth.refreshToken;
      if (rt) {
        this.api.refreshToken(rt).subscribe({
          next: (res) => {
            this.auth.setUser({
              accessToken: res.accessToken,
              refreshToken: res.refreshToken,
              userId: res.userId,
              displayName: res.displayName,
              userType: res.userType,
            });
          },
          error: (err) => {
            if (err.status === 401 || err.status === 403) {
              this.auth.logout();
            }
          },
        });
      }
    }
    this.notifications.initialize();

    // Initialize SignalR connection and order notifications
    this.signalr
      .startConnection()
      .then(() => {
        this.orderNotifications.initialize();
      })
      .catch((error) => {
        console.error('Failed to start SignalR connection:', error);
      });

  }

  ngOnDestroy(): void {
    this.signalr.stopConnection();
  }
}
