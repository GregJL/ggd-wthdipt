import { archivedLocationIds } from './searchFilters.ts';
import type { InventoryItem, Location } from './types';

export function organizationView(items: InventoryItem[], locations: Location[]) {
  const archived = archivedLocationIds(locations);
  const byId = new Map(locations.map((location) => [location.id, location]));
  const staging = new Set<string>();
  for (const location of locations) {
    if (archived.has(location.id)) continue;
    let cursor: Location | undefined = location; const seen = new Set<string>();
    while (cursor && !seen.has(cursor.id)) {
      if (cursor.isStaging) { staging.add(location.id); break; }
      seen.add(cursor.id); cursor = cursor.parentId ? byId.get(cursor.parentId) : undefined;
    }
  }
  const activeItems = items.filter((item) => !archived.has(item.locationId));
  return { archived, staging, activeItems,
    needsSorting: activeItems.filter((item) => item.needsSorting),
    staged: activeItems.filter((item) => staging.has(item.locationId)),
    planned: activeItems.filter((item) => item.plannedLocationId),
    attention: activeItems.filter((item) => item.needsSorting || staging.has(item.locationId) || item.plannedLocationId),
  };
}
