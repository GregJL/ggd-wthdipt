const developmentApiPort = window.location.protocol === 'https:' ? '5001' : '5000';
const defaultApiUrl = import.meta.env.DEV
  ? `${window.location.protocol}//${window.location.hostname}:${developmentApiPort}/api`
  : `${window.location.origin}/api`;
export const API_URL = import.meta.env.VITE_API_URL || defaultApiUrl;
let csrfToken = '';
export function setCsrfToken(token: string) { csrfToken = token; }

export async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const isForm = options?.body instanceof FormData;
  const mutates = options?.method && !['GET', 'HEAD', 'OPTIONS'].includes(options.method.toUpperCase());
  const response = await fetch(`${API_URL}${path}`, {
    ...options, credentials: 'include', cache: 'no-store',
    headers: { ...(isForm ? {} : { 'Content-Type': 'application/json' }), ...(mutates ? { 'X-GGD-CSRF': csrfToken } : {}), ...options?.headers },
  });
  if (!response.ok) {
    if (response.status === 401 && !path.startsWith('/auth/')) window.dispatchEvent(new Event('ggd-auth-expired'));
    let detail = await response.text();
    try { const parsed = JSON.parse(detail); if (typeof parsed === 'string') detail = parsed; } catch { /* Plain-text API message. */ }
    throw new Error(response.status === 429 ? 'Too many attempts. Please wait a few minutes and try again.'
      : detail && detail.length < 500 && !detail.startsWith('{') && !detail.startsWith('<') ? detail : `Request failed (${response.status}). Please try again.`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
