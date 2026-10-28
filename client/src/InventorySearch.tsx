import { useEffect, useMemo, useState } from 'react';
import { Box, ChevronRight, MapPin } from 'lucide-react';
import { WORKSPACE_ID } from './data/seed';
import { store } from './data/store';
import { emptyFilters, filterInventory } from './searchFilters';
import type { Choice, PhotoTargets, SearchFilters } from './searchFilters';
import type { InventoryItem, Location } from './types';

export default function InventorySearch({ items, locations, query, loading, pathForLocation, onOpen, onClose }: {
  items: InventoryItem[]; locations: Location[]; query: string; loading: boolean;
  pathForLocation: (id: string) => string; onOpen: (id: string) => void; onClose: () => void;
}) {
  const [filters, setFilters] = useState<SearchFilters>(emptyFilters);
  const [targets, setTargets] = useState<PhotoTargets | null>(null);
  const [photoError, setPhotoError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (filters.photo === 'all' || targets !== null) return;
    let cancelled = false;
    setPhotoError('');
    store.photoTargets(WORKSPACE_ID).then((result) => { if (!cancelled) setTargets(result); })
      .catch(() => { if (!cancelled) setPhotoError('Could not check which entries have photos.'); });
    return () => { cancelled = true; };
  }, [filters.photo, targets, attempt]);
  const results = useMemo(() => filterInventory(items, locations, query, filters, targets), [items, locations, query, filters, targets]);
  const busy = loading || (filters.photo !== 'all' && targets === null && !photoError);
  const failed = filters.photo !== 'all' && !!photoError;
  const count = results.items.length + results.locations.length;
  const choose = (key: keyof SearchFilters, value: string) => setFilters((known) => ({ ...known, [key]: value as Choice }));
  return <section className="search-results">
    <div className="section-heading"><div><h2>Search results</h2><p role="status">{busy ? 'Checking inventory…' : failed ? 'Photo filter unavailable.' : `${count} result${count === 1 ? '' : 's'} found.`}</p></div><button className="search-close" onClick={onClose}>Close search</button></div>
    <fieldset className="search-filters"><legend>Filter results</legend>
      <label>Location status<select value={filters.archive} onChange={(e) => choose('archive', e.target.value)}><option value="all">All locations</option><option value="no">Not archived</option><option value="yes">Archived</option></select></label>
      <label>Edit lock<select value={filters.editLock} onChange={(e) => choose('editLock', e.target.value)}><option value="all">Any</option><option value="yes">Locked</option><option value="no">Unlocked</option></select></label>
      <label>Move lock<select value={filters.moveLock} onChange={(e) => choose('moveLock', e.target.value)}><option value="all">Any</option><option value="yes">Locked</option><option value="no">Unlocked</option></select></label>
      <label>Photos<select value={filters.photo} onChange={(e) => choose('photo', e.target.value)}><option value="all">Any</option><option value="yes">With photos</option><option value="no">Without photos</option></select></label>
      <button className="search-reset" onClick={() => setFilters(emptyFilters)}>Reset filters</button>
      <p>Filters combine. Archived includes contents of archived locations. Photos and locks apply to each result itself.</p>
    </fieldset>
    {failed && <div role="alert" className="error-banner"><span>{photoError}</span><button onClick={() => { setPhotoError(''); setAttempt((value) => value + 1); }}>Retry</button></div>}
    {!busy && !failed && <>
      {!count && <p>No matches. Try a different search or reset the filters.</p>}
      {results.locations.length > 0 && <section aria-label="Matching locations"><h3>Locations <span>{results.locations.length}</span></h3><div className="cards">
        {results.locations.map((location) => <button className={`location-card ${results.archived.has(location.id) ? 'archived-card' : ''}`} key={location.id} onClick={() => onOpen(location.id)}><span className="icon"><MapPin size={22}/></span><span><strong>{location.name}</strong><small>{pathForLocation(location.id)}{results.archived.has(location.id) ? ' · Archived' : ''}</small></span><ChevronRight size={21}/></button>)}
      </div></section>}
      {results.items.length > 0 && <section aria-label="Matching items"><h3>Items <span>{results.items.length}</span></h3><div className="cards">
        {results.items.map((item) => <button className={`location-card ${results.archived.has(item.locationId) ? 'archived-card' : ''}`} key={item.id} onClick={() => onOpen(item.locationId)}><span className="icon"><Box size={22}/></span><span><strong>{item.name}</strong><small>{pathForLocation(item.locationId)} · Qty {item.quantity}{results.archived.has(item.locationId) ? ' · Archived' : ''}</small></span><ChevronRight size={21}/></button>)}
      </div></section>}
    </>}
  </section>;
}
