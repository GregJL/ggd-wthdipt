import type { InventoryItem, Location } from './types';

export type Choice = 'all' | 'yes' | 'no';
export interface SearchFilters {
  archive: Choice;
  editLock: Choice;
  moveLock: Choice;
  photo: Choice;
}
export interface PhotoTargets { itemIds: string[]; locationIds: string[] }
export const emptyFilters: SearchFilters = { archive: 'all', editLock: 'all', moveLock: 'all', photo: 'all' };

export function archivedLocationIds(locations: Location[]): Set<string> {
  const byId = new Map(locations.map((location) => [location.id, location]));
  return new Set(locations.filter((location) => {
    let cursor: Location | undefined = location;
    const visited = new Set<string>();
    while (cursor && !visited.has(cursor.id)) {
      if (cursor.status === 'inactive') return true;
      visited.add(cursor.id);
      cursor = cursor.parentId ? byId.get(cursor.parentId) : undefined;
    }
    return false;
  }).map((location) => location.id));
}

export function filterInventory(items: InventoryItem[], locations: Location[], query: string, filters: SearchFilters, targets: PhotoTargets | null) {
  const term = query.trim().toLocaleLowerCase();
  const archived = archivedLocationIds(locations);
  const photographedItems = new Set(targets?.itemIds);
  const photographedLocations = new Set(targets?.locationIds);
  const matchesChoice = (choice: Choice, value: boolean) => choice === 'all' || value === (choice === 'yes');
  const matches = (entry: InventoryItem | Location, isArchived: boolean, hasPhoto: boolean) =>
    `${entry.name} ${entry.note ?? ''}`.toLocaleLowerCase().includes(term)
    && matchesChoice(filters.archive, isArchived)
    && matchesChoice(filters.editLock, entry.isEditLocked)
    && matchesChoice(filters.moveLock, entry.isMoveLocked)
    && matchesChoice(filters.photo, hasPhoto);
  // Unknown photo presence must never be treated as "without photos".
  if (filters.photo !== 'all' && targets === null) return { items: [], locations: [], archived };
  return {
    items: items.filter((item) => matches(item, archived.has(item.locationId), photographedItems.has(item.id))),
    locations: locations.filter((location) => matches(location, archived.has(location.id), photographedLocations.has(location.id))),
    archived,
  };
}
