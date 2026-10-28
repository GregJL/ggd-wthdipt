export type LocationStatus = 'active' | 'planned' | 'temporary' | 'inactive';

export interface Location {
  isStaging?: boolean;
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

export interface ItemMetadata {
  category?: string | null;
  tags?: string[];
  brand?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  purchaseDate?: string | null;
  purchasePrice?: number | null;
  replacementValue?: number | null;
  warrantyExpiresOn?: string | null;
  warrantyNotes?: string | null;
  condition?: string | null;
}
export interface ItemDraft extends ItemMetadata { name: string; quantity: number; note?: string }
export interface InventoryItem extends ItemMetadata {
  needsSorting?: boolean;
  plannedLocationId?: string | null;
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
  plannedItemCount?: number;
  locationId: string;
  isRoot: boolean;
  childLocationCount: number;
  itemCount: number;
  photoCount: number;
  movementReferenceCount: number;
  canDelete: boolean;
}

export type PhotoStage = 'general' | 'before' | 'after';
export interface InventoryPhoto {
  stage?: PhotoStage;
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

export interface ActivitySubject { kind: 'item' | 'location'; id: string; name: string }
export interface ActivityEntry {
  actorUserId: string | null;
  actorName: string | null;
  id: number;
  workspaceId: string;
  subjectType: 'item' | 'location' | 'task';
  subjectId: string;
  subjectName: string;
  action: string;
  summary: string;
  details: string;
  locationPath: string;
  recordedAt: string;
}
export interface ActivityPage { entries: ActivityEntry[]; hasMore: boolean }

export interface AccountUser { id: string; email: string; displayName: string; workspaceId: string }
export interface AccountSession { recoveryAvailable: boolean; user: AccountUser | null; setupRequired: boolean; setupAvailable: boolean; csrfToken: string }

export interface OrganizationTaskDraft { title: string; note: string | null; itemId: string | null; locationId: string | null; isCompleted: boolean }
export interface OrganizationTask extends OrganizationTaskDraft { id: string; workspaceId: string; referenceName: string | null; createdAt: string; completedAt: string | null }
