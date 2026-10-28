import { request, setCsrfToken } from './http';
import type { AccountSession } from '../types';

let pendingSession: Promise<AccountSession> | null = null;
export const auth = {
  session(): Promise<AccountSession> {
    // Share initial requests, including React development's double effect run.
    return pendingSession ??= request<AccountSession>('/auth/session').then((session) => {
      setCsrfToken(session.csrfToken); return session;
    }).finally(() => { pendingSession = null; });
  },
  login(email: string, password: string, rememberMe: boolean) {
    return request<void>('/auth/login', { method: 'POST', body: JSON.stringify({ email, password, rememberMe }) });
  },
  setup(email: string, displayName: string, password: string, setupCode: string) {
    return request<void>('/auth/setup', { method: 'POST', body: JSON.stringify({ email, displayName, password, setupCode }) });
  },
  logout() { return request<void>('/auth/logout', { method: 'POST' }); },
  changePassword(currentPassword: string, newPassword: string) {
    return request<void>('/auth/password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) });
  },
};
