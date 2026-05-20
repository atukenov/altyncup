import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface AuthUser {
  token: string;
  userId: string;
  displayName: string;
  userType: string;
  role?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly TOKEN_KEY = 'yurt_token';
  private readonly USER_KEY = 'yurt_user';

  private _user$ = new BehaviorSubject<AuthUser | null>(this.loadFromStorage());
  readonly user$ = this._user$.asObservable();

  get currentUser(): AuthUser | null {
    return this._user$.getValue();
  }
  get token(): string | null {
    return this.currentUser?.token ?? null;
  }
  get isLoggedIn(): boolean {
    return !!this.currentUser;
  }
  get isAdmin(): boolean {
    return this.currentUser?.role === 'Admin';
  }

  setUser(user: AuthUser): void {
    this._user$.next(user);
    localStorage.setItem(this.TOKEN_KEY, user.token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
  }

  logout(): void {
    this._user$.next(null);
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
  }

  private loadFromStorage(): AuthUser | null {
    try {
      const raw = localStorage.getItem(this.USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }
}
