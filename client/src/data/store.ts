import type { InventoryItem, InventoryPhoto, Location, LocationDeletionImpact, MovementHistoryEntry } from '../types';

const developmentApiPort = window.location.protocol === 'https:' ? '5001' : '5000';
const defaultApiUrl = import.meta.env.DEV
  ? `${window.location.protocol}//${window.location.hostname}:${developmentApiPort}/api`
  : `${window.location.origin}/api`;
const API_URL = import.meta.env.VITE_API_URL || defaultApiUrl;

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const isForm = options?.body instanceof FormData;
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: { ...(isForm ? {} : { 'Content-Type': 'application/json' }), ...options?.headers },
  });
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Request failed with status ${response.status}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const store = {
  locations(workspaceId: string, parentId: string | null): Promise<Location[]> {
    const query = new URLSearchParams({ workspaceId });
    if (parentId) query.set('parentId', parentId);
    return request<Location[]>(`/locations?${query}`);
  },
  allLocations(workspaceId: string): Promise<Location[]> {
    return request<Location[]>(`/locations/all?${new URLSearchParams({ workspaceId })}`);
  },
  items(workspaceId: string): Promise<InventoryItem[]> {
    return request<InventoryItem[]>(`/items?${new URLSearchParams({ workspaceId })}`);
  },
  addLocation(location: Omit<Location, 'id'>): Promise<Location> {
    return request<Location>('/locations', { method: 'POST', body: JSON.stringify(location) });
  },
  updateLocation(location: Location): Promise<Location> {
    return request<Location>(`/locations/${location.id}`, { method: 'PATCH', body: JSON.stringify({ workspaceId: location.workspaceId, name: location.name, type: location.type, status: location.status, note: location.note }) });
  },
  addItem(item: Omit<InventoryItem, 'id' | 'createdAt'>): Promise<InventoryItem> {
    return request<InventoryItem>('/items', { method: 'POST', body: JSON.stringify(item) });
  },
  updateItem(item: InventoryItem): Promise<InventoryItem> {
    return request<InventoryItem>(`/items/${item.id}`, { method: 'PATCH', body: JSON.stringify({ workspaceId: item.workspaceId, name: item.name, quantity: item.quantity, note: item.note }) });
  },
  deleteItem(workspaceId: string, itemId: string): Promise<void> {
    const query = new URLSearchParams({ workspaceId });
    return request<void>(`/items/${itemId}?${query}`, { method: 'DELETE' });
  },
  moveItem(workspaceId: string, itemId: string, destinationLocationId: string): Promise<InventoryItem> {
    return request<InventoryItem>(`/items/${itemId}/move`, { method: 'PATCH', body: JSON.stringify({ workspaceId, destinationLocationId }) });
  },
  setItemMoveLock(workspaceId: string, itemId: string, isMoveLocked: boolean): Promise<InventoryItem> {
    return request<InventoryItem>(`/items/${itemId}/move-lock`, { method: 'PATCH', body: JSON.stringify({ workspaceId, isMoveLocked }) });
  },
  setItemEditLock(workspaceId: string, itemId: string, isEditLocked: boolean): Promise<InventoryItem> {
    return request<InventoryItem>(`/items/${itemId}/edit-lock`, { method: 'PATCH', body: JSON.stringify({ workspaceId, isEditLocked }) });
  },
  moveLocation(workspaceId: string, locationId: string, destinationParentId: string): Promise<Location> {
    return request<Location>(`/locations/${locationId}/move`, { method: 'PATCH', body: JSON.stringify({ workspaceId, destinationParentId }) });
  },
  setLocationMoveLock(workspaceId: string, locationId: string, isMoveLocked: boolean): Promise<Location> {
    return request<Location>(`/locations/${locationId}/move-lock`, { method: 'PATCH', body: JSON.stringify({ workspaceId, isMoveLocked }) });
  },
  setLocationEditLock(workspaceId: string, locationId: string, isEditLocked: boolean): Promise<Location> {
    return request<Location>(`/locations/${locationId}/edit-lock`, { method: 'PATCH', body: JSON.stringify({ workspaceId, isEditLocked }) });
  },
  locationDeletionImpact(workspaceId: string, locationId: string): Promise<LocationDeletionImpact> {
    return request<LocationDeletionImpact>(`/locations/${locationId}/deletion-impact?${new URLSearchParams({ workspaceId })}`);
  },
  archiveLocation(workspaceId: string, locationId: string): Promise<Location> {
    return request<Location>(`/locations/${locationId}/archive`, { method: 'PATCH', body: JSON.stringify({ workspaceId }) });
  },
  restoreLocation(workspaceId: string, locationId: string): Promise<Location> {
    return request<Location>(`/locations/${locationId}/restore`, { method: 'PATCH', body: JSON.stringify({ workspaceId }) });
  },
  deleteLocation(workspaceId: string, locationId: string): Promise<void> {
    return request<void>(`/locations/${locationId}?${new URLSearchParams({ workspaceId })}`, { method: 'DELETE' });
  },
  itemHistory(workspaceId: string, itemId: string): Promise<MovementHistoryEntry[]> {
    return request<MovementHistoryEntry[]>(`/items/${itemId}/history?${new URLSearchParams({ workspaceId })}`);
  },
  locationHistory(workspaceId: string, locationId: string): Promise<MovementHistoryEntry[]> {
    return request<MovementHistoryEntry[]>(`/locations/${locationId}/history?${new URLSearchParams({ workspaceId })}`);
  },
  photos(workspaceId: string, target: { itemId: string } | { locationId: string }): Promise<InventoryPhoto[]> {
    return request<InventoryPhoto[]>(`/photos?${new URLSearchParams({ workspaceId, ...target })}`);
  },
  uploadPhoto(workspaceId: string, target: { itemId: string } | { locationId: string }, file: File, caption: string): Promise<InventoryPhoto> {
    const body = new FormData(); body.set('workspaceId', workspaceId); body.set('file', file); body.set('caption', caption);
    if ('itemId' in target) body.set('itemId', target.itemId); else body.set('locationId', target.locationId);
    return request<InventoryPhoto>('/photos', { method: 'POST', body });
  },
  updatePhoto(workspaceId: string, photoId: string, caption: string): Promise<InventoryPhoto> {
    return request<InventoryPhoto>(`/photos/${photoId}`, { method: 'PATCH', body: JSON.stringify({ workspaceId, caption }) });
  },
  deletePhoto(workspaceId: string, photoId: string): Promise<void> {
    return request<void>(`/photos/${photoId}?${new URLSearchParams({ workspaceId })}`, { method: 'DELETE' });
  },
  photoUrl(contentUrl: string): string {
    return `${API_URL.replace(/\/api$/, '')}${contentUrl}`;
  },
};
