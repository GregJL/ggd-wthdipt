export type LocationStatus = 'active' | 'planned' | 'temporary' | 'inactive';

export interface Location {
  id: string;
  workspaceId: string;
  parentId: string | null;
  name: string;
  type: 'floor' | 'room' | 'area' | 'storage';
  status: LocationStatus;
  isMoveLocked: boolean;
  isEditLocked: boolean;
  note?: string;
}

export interface InventoryItem {
  id: string;
  workspaceId: string;
  locationId: string;
  name: string;
  quantity: number;
  isMoveLocked: boolean;
  isEditLocked: boolean;
  note?: string;
  createdAt: string;
}

export interface LocationDeletionImpact {
  locationId: string;
  isRoot: boolean;
  childLocationCount: number;
  itemCount: number;
  photoCount: number;
  movementReferenceCount: number;
  canDelete: boolean;
}

export interface InventoryPhoto {
  id: string;
  workspaceId: string;
  itemId: string | null;
  locationId: string | null;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  caption?: string;
  createdAt: string;
  contentUrl: string;
}

export interface MovementHistoryEntry {
  id: string;
  fromLocationId: string;
  fromLocationName: string;
  toLocationId: string;
  toLocationName: string;
  movedAt: string;
}
