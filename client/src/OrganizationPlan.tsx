import { useState } from 'react';
import type { InventoryItem, Location } from './types';
import { archivedLocationIds } from './searchFilters';

export default function OrganizationPlan({ item, locations, path, onSave }: {
  item: InventoryItem; locations: Location[]; path: (id: string) => string;
  onSave: (needsSorting: boolean, plannedLocationId: string | null) => Promise<void>;
}) {
  const [needsSorting, setNeedsSorting] = useState(item.needsSorting ?? false);
  const [destination, setDestination] = useState(item.plannedLocationId ?? '');
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const archived = archivedLocationIds(locations);
  const choices = locations.filter((location) => !archived.has(location.id) && location.id !== item.locationId).sort((a, b) => path(a.id).localeCompare(path(b.id)));
  const unavailable = destination && !choices.some((choice) => choice.id === destination);
  return <form onSubmit={async (event) => {
    event.preventDefault(); setBusy(true); setError('');
    try { await onSave(needsSorting, destination || null); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not save this plan.'); }
    finally { setBusy(false); }
  }}>
    <p>Currently in {path(item.locationId)}. Saving a plan leaves the item here.</p>
    <label className="organization-check"><input type="checkbox" checked={needsSorting} onChange={(e) => setNeedsSorting(e.target.checked)}/> Needs sorting</label>
    <label>Planned destination<select value={destination} onChange={(e) => setDestination(e.target.value)}><option value="">No planned destination</option>
      {unavailable && <option value={destination} disabled>{path(destination)} — choose another destination or clear the plan</option>}
      {choices.map((location) => <option value={location.id} key={location.id}>{path(location.id)}</option>)}
    </select></label>
    {error && <p role="alert">{error}</p>}
    <button className="submit" disabled={busy || item.isEditLocked}>{busy ? 'Saving…' : 'Save organization plan'}</button>
  </form>;
}
