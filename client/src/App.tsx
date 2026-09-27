import { useEffect, useMemo, useState } from 'react';
import { Archive, ArrowLeft, Box, Camera, ChevronRight, Clock3, ExternalLink, History, Home, Image as ImageIcon, Lock, MapPin, MoveRight, Pencil, PencilOff, Plus, RefreshCw, RotateCcw, Search, Trash2, Unlock, Upload, WifiOff, X } from 'lucide-react';
import { WORKSPACE_ID } from './data/seed';
import { store } from './data/store';
import { applyPwaUpdate } from './pwa';
import type { InventoryItem, InventoryPhoto, Location, LocationDeletionImpact, LocationStatus, MovementHistoryEntry } from './types';

type Modal = 'location' | 'item' | 'editItem' | 'editLocation' | 'moveItem' | 'moveLocation' | 'removeLocation' | 'history' | 'photos' | null;
type PhotoTarget = { kind: 'item'; id: string; name: string } | { kind: 'location'; id: string; name: string };

export default function App() {
  const [locations, setLocations] = useState<Location[]>([]);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [currentId, setCurrentId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [modal, setModal] = useState<Modal>(null);
  const [selectedItem, setSelectedItem] = useState<InventoryItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showArchived, setShowArchived] = useState(false);
  const [deletionImpact, setDeletionImpact] = useState<LocationDeletionImpact | null>(null);
  const [history, setHistory] = useState<MovementHistoryEntry[] | null>(null);
  const [historyTitle, setHistoryTitle] = useState('');
  const [photoTarget, setPhotoTarget] = useState<PhotoTarget | null>(null);
  const [photos, setPhotos] = useState<InventoryPhoto[] | null>(null);
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [updateAvailable, setUpdateAvailable] = useState(false);
  const current = locations.find((location) => location.id === currentId);
  const children = locations.filter((location) => location.parentId === currentId && (showArchived || location.status !== 'inactive'));
  const archivedChildCount = locations.filter((location) => location.parentId === currentId && location.status === 'inactive').length;
  const crumbs = useMemo(() => {
    const result: Location[] = [];
    let cursor = current;
    while (cursor) {
      result.unshift(cursor);
      cursor = locations.find((location) => location.id === cursor?.parentId);
    }
    return result;
  }, [current, locations]);
  const visibleItems = items.filter((item) => item.locationId === currentId);
  const searchTerm = query.trim().toLocaleLowerCase();
  const matchingItems = searchTerm
    ? items.filter((item) => `${item.name} ${item.note ?? ''}`.toLocaleLowerCase().includes(searchTerm))
    : [];
  const matchingLocations = searchTerm
    ? locations.filter((location) => `${location.name} ${location.note ?? ''}`.toLocaleLowerCase().includes(searchTerm))
    : [];

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError('');
    Promise.all([store.allLocations(WORKSPACE_ID), store.items(WORKSPACE_ID)])
      .then(([nextLocations, nextItems]) => {
        if (cancelled) return;
        setLocations(nextLocations);
        setItems(nextItems);
      })
      .catch((reason: unknown) => !cancelled && setError(reason instanceof Error ? reason.message : 'Could not reach the inventory API.'))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const online = () => setIsOnline(true); const offline = () => setIsOnline(false); const update = () => setUpdateAvailable(true);
    window.addEventListener('online', online); window.addEventListener('offline', offline); window.addEventListener('ggd-pwa-update', update);
    return () => { window.removeEventListener('online', online); window.removeEventListener('offline', offline); window.removeEventListener('ggd-pwa-update', update); };
  }, []);

  function goBack() { setCurrentId(current?.parentId ?? null); }
  async function addLocation(name: string, type: Location['type'], status: LocationStatus, note: string) {
    try {
      const added = await store.addLocation({ workspaceId: WORKSPACE_ID, parentId: currentId, name, type, status, isMoveLocked: false, isEditLocked: false, note: note || undefined });
      setLocations((known) => [...known, added]); setModal(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not add the location.'); }
  }
  async function addItem(name: string, quantity: number, note: string) {
    if (!currentId) return;
    try {
      const added = await store.addItem({ workspaceId: WORKSPACE_ID, locationId: currentId, name, quantity, isMoveLocked: false, isEditLocked: false, note: note || undefined });
      setItems((known) => [...known, added]); setModal(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not add the item.'); }
  }
  async function editLocation(name: string, type: Location['type'], status: LocationStatus, note: string) {
    if (!current) return;
    try {
      const updated = await store.updateLocation({ ...current, name, type, status, note: note || undefined });
      setLocations((known) => known.map((location) => location.id === updated.id ? updated : location));
      setModal(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not update the location.'); }
  }
  async function editItem(name: string, quantity: number, note: string) {
    if (!selectedItem) return;
    try {
      const updated = await store.updateItem({ ...selectedItem, name, quantity, note: note || undefined });
      setItems((known) => known.map((item) => item.id === updated.id ? updated : item));
      setSelectedItem(null); setModal(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not update the item.'); }
  }
  async function deleteItem(item: InventoryItem) {
    if (!window.confirm(`Delete “${item.name}” and any photos attached to it? This cannot be undone.`)) return;
    setError('');
    try {
      await store.deleteItem(WORKSPACE_ID, item.id);
      setItems((known) => known.filter((candidate) => candidate.id !== item.id));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not delete the item.'); }
  }
  async function moveItem(destinationLocationId: string) {
    if (!selectedItem) return;
    try {
      const moved = await store.moveItem(WORKSPACE_ID, selectedItem.id, destinationLocationId);
      setItems((known) => known.map((item) => item.id === moved.id ? moved : item));
      setSelectedItem(null); setModal(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not move the item.'); }
  }
  async function toggleItemLock(item: InventoryItem) {
    try {
      const updated = await store.setItemMoveLock(WORKSPACE_ID, item.id, !item.isMoveLocked);
      setItems((known) => known.map((candidate) => candidate.id === updated.id ? updated : candidate));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not change the item lock.'); }
  }
  async function toggleItemEditLock(item: InventoryItem) {
    try {
      const updated = await store.setItemEditLock(WORKSPACE_ID, item.id, !item.isEditLocked);
      setItems((known) => known.map((candidate) => candidate.id === updated.id ? updated : candidate));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not change the item edit lock.'); }
  }
  async function moveLocation(destinationParentId: string) {
    if (!current) return;
    try {
      const moved = await store.moveLocation(WORKSPACE_ID, current.id, destinationParentId);
      setLocations((known) => known.map((location) => location.id === moved.id ? moved : location));
      setModal(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not move the location.'); }
  }
  async function toggleLocationLock() {
    if (!current) return;
    try {
      const updated = await store.setLocationMoveLock(WORKSPACE_ID, current.id, !current.isMoveLocked);
      setLocations((known) => known.map((location) => location.id === updated.id ? updated : location));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not change the location lock.'); }
  }
  async function toggleLocationEditLock() {
    if (!current) return;
    try {
      const updated = await store.setLocationEditLock(WORKSPACE_ID, current.id, !current.isEditLocked);
      setLocations((known) => known.map((location) => location.id === updated.id ? updated : location));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not change the location edit lock.'); }
  }
  async function openRemoveLocation() {
    if (!current) return;
    setError(''); setDeletionImpact(null); setModal('removeLocation');
    try { setDeletionImpact(await store.locationDeletionImpact(WORKSPACE_ID, current.id)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not check whether this location can be removed.'); setModal(null); }
  }
  async function archiveLocation() {
    if (!current) return;
    try {
      const parentId = current.parentId;
      const updated = await store.archiveLocation(WORKSPACE_ID, current.id);
      setLocations((known) => known.map((location) => location.id === updated.id ? updated : location));
      setModal(null); setCurrentId(parentId);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not archive the location.'); }
  }
  async function restoreLocation() {
    if (!current || !window.confirm(`Restore “${current.name}” to active locations?`)) return;
    try {
      const updated = await store.restoreLocation(WORKSPACE_ID, current.id);
      setLocations((known) => known.map((location) => location.id === updated.id ? updated : location));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not restore the location.'); }
  }
  async function deleteLocation() {
    if (!current || !deletionImpact?.canDelete) return;
    try {
      const parentId = current.parentId;
      await store.deleteLocation(WORKSPACE_ID, current.id);
      setLocations((known) => known.filter((location) => location.id !== current.id));
      setModal(null); setCurrentId(parentId);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not delete the location.'); }
  }
  async function openItemHistory(item: InventoryItem) {
    setSelectedItem(item); setHistoryTitle(item.name); setHistory(null); setError(''); setModal('history');
    try { setHistory(await store.itemHistory(WORKSPACE_ID, item.id)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not load the item history.'); setModal(null); setSelectedItem(null); }
  }
  async function openLocationHistory() {
    if (!current) return;
    setHistoryTitle(current.name); setHistory(null); setError(''); setModal('history');
    try { setHistory(await store.locationHistory(WORKSPACE_ID, current.id)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not load the location history.'); setModal(null); }
  }
  function closeHistory() { setHistory(null); setSelectedItem(null); setModal(null); }
  async function openPhotos(target: PhotoTarget) {
    setPhotoTarget(target); setPhotos(null); setError(''); setModal('photos');
    try { setPhotos(await store.photos(WORKSPACE_ID, target.kind === 'item' ? { itemId: target.id } : { locationId: target.id })); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not load the photos.'); setModal(null); setPhotoTarget(null); }
  }
  async function uploadPhoto(file: File, caption: string) {
    if (!photoTarget) return;
    const uploaded = await store.uploadPhoto(WORKSPACE_ID, photoTarget.kind === 'item' ? { itemId: photoTarget.id } : { locationId: photoTarget.id }, file, caption);
    setPhotos((known) => [uploaded, ...(known ?? [])]);
  }
  async function updatePhoto(photo: InventoryPhoto, caption: string) {
    const updated = await store.updatePhoto(WORKSPACE_ID, photo.id, caption);
    setPhotos((known) => known?.map((candidate) => candidate.id === updated.id ? updated : candidate) ?? []);
  }
  async function deletePhoto(photo: InventoryPhoto) {
    if (!window.confirm(`Delete this photo${photo.caption ? ` (“${photo.caption}”)` : ''}? This cannot be undone.`)) return;
    await store.deletePhoto(WORKSPACE_ID, photo.id);
    setPhotos((known) => known?.filter((candidate) => candidate.id !== photo.id) ?? []);
  }
  function closePhotos() { setPhotos(null); setPhotoTarget(null); setModal(null); }

  return <div className="app-shell">
    <header>
      <div className="eyebrow">Greg's giant database of</div>
      <h1>Where the hell did I put that?</h1>
      <label className="search"><Search size={20} /><input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Find anything…" aria-label="Search inventory" />{query && <button onClick={() => setQuery('')} aria-label="Clear search"><X size={18}/></button>}</label>
    </header>

    <main>
      {!isOnline && <div className="pwa-banner offline-banner"><WifiOff size={19}/><div><strong>You’re offline</strong><span>The app shell is available, but inventory changes and photos need the home server.</span></div></div>}
      {updateAvailable && <div className="pwa-banner update-banner"><RefreshCw size={19}/><div><strong>An update is ready</strong><span>Reload to use the newest version.</span></div><button onClick={applyPwaUpdate}>Reload</button></div>}
      {error && <div className="error-banner" role="alert"><strong>Inventory connection problem</strong><span>{error}</span></div>}
      {loading && !locations.length && <div className="loading">Opening the inventory…</div>}
      {searchTerm ? <SearchResults items={matchingItems} matches={matchingLocations} locations={locations} onOpen={(id) => { setCurrentId(id); setShowArchived(true); setQuery(''); }} /> : <>
        <nav className="breadcrumb" aria-label="Breadcrumb">
          <button onClick={() => setCurrentId(null)}><Home size={17} /> Home</button>
          {crumbs.map((crumb) => <span key={crumb.id}><ChevronRight size={15}/><button onClick={() => setCurrentId(crumb.id)}>{crumb.name}</button></span>)}
        </nav>
        <section className="section-heading">
          <div>{current && <button className="back" onClick={goBack}><ArrowLeft size={20}/> Back</button>}<h2>{current?.name ?? 'Your Home'}</h2><p>{current?.note ?? (current ? 'Choose a location or see what is stored here.' : 'Choose a floor to start exploring.')}</p></div>
          {current?.status && current.status !== 'active' && <span className={`status ${current.status}`}>{current.status}</span>}
        </section>
        {current?.status === 'inactive' ? <div className="location-actions"><button onClick={() => openPhotos({ kind: 'location', id: current.id, name: current.name })}><Camera size={17}/> Photos</button><button onClick={openLocationHistory}><History size={17}/> Movement history</button><button className="restore-button" onClick={restoreLocation}><RotateCcw size={17}/> Restore location</button></div> : current && <div className="location-actions">
          <button onClick={() => openPhotos({ kind: 'location', id: current.id, name: current.name })}><Camera size={17}/> Photos</button>
          <button onClick={openLocationHistory}><History size={17}/> Movement history</button>
          <button disabled={current.isEditLocked} onClick={() => setModal('editLocation')}><Pencil size={17}/> Edit location</button>
          <button onClick={toggleLocationEditLock}>{current.isEditLocked ? <><Pencil size={17}/> Unlock edits</> : <><PencilOff size={17}/> Lock edits</>}</button>
          <button onClick={toggleLocationLock}>{current.isMoveLocked ? <><Unlock size={17}/> Unlock location</> : <><Lock size={17}/> Lock location</>}</button>
          <button disabled={current.parentId === null || current.isMoveLocked} onClick={() => setModal('moveLocation')}><MoveRight size={17}/> Move location</button>
          <button className="remove-button" disabled={current.parentId === null} onClick={openRemoveLocation}><Archive size={17}/> Archive or delete</button>
        </div>}

        {archivedChildCount > 0 && <button className="archived-toggle" onClick={() => setShowArchived((shown) => !shown)}>{showArchived ? 'Hide archived' : `Show archived (${archivedChildCount})`}</button>}

        <div className="cards">
          {children.map((location) => <button className={`location-card ${location.status === 'inactive' ? 'archived-card' : ''}`} key={location.id} onClick={() => setCurrentId(location.id)}>
            <span className="icon"><MapPin size={22}/></span><span><strong>{location.name}</strong><small>{location.type}{location.status !== 'active' ? ` · ${location.status}` : ''}</small></span><ChevronRight size={21}/>
          </button>)}
        </div>

        {current && <section className="items"><div className="items-title"><h3>Items here</h3><span>{visibleItems.length}</span></div>
          {visibleItems.length ? visibleItems.map((item) => <article key={item.id}><Box size={22}/><div><strong>{item.name}</strong><small>Qty {item.quantity}{item.note ? ` · ${item.note}` : ''}</small></div><div className="item-actions"><button onClick={() => openPhotos({ kind: 'item', id: item.id, name: item.name })} aria-label={`Photos for ${item.name}`} title="Photos"><Camera size={19}/></button><button onClick={() => openItemHistory(item)} aria-label={`Movement history for ${item.name}`} title="Movement history"><History size={19}/></button><button disabled={item.isEditLocked} onClick={() => { setSelectedItem(item); setModal('editItem'); }} aria-label={`Edit ${item.name}`} title={`Edit ${item.name}`}><Pencil size={19}/></button><button onClick={() => toggleItemEditLock(item)} aria-label={`${item.isEditLocked ? 'Unlock' : 'Lock'} editing for ${item.name}`} title={`${item.isEditLocked ? 'Unlock' : 'Lock'} editing`}>{item.isEditLocked ? <Pencil size={19}/> : <PencilOff size={19}/>}</button><button onClick={() => toggleItemLock(item)} aria-label={`${item.isMoveLocked ? 'Unlock' : 'Lock'} movement for ${item.name}`} title={`${item.isMoveLocked ? 'Unlock' : 'Lock'} movement`}>{item.isMoveLocked ? <Lock size={19}/> : <Unlock size={19}/>}</button><button disabled={item.isMoveLocked} onClick={() => { setSelectedItem(item); setModal('moveItem'); }} aria-label={`Move ${item.name}`} title={`Move ${item.name}`}><MoveRight size={19}/></button><button className="delete-item" onClick={() => deleteItem(item)} aria-label={`Delete ${item.name}`} title={`Delete ${item.name}`}><Trash2 size={19}/></button></div></article>) : <div className="empty"><Box size={32}/><p>Nothing inventoried here yet.</p><span>That does not mean nothing is hiding here.</span></div>}
        </section>}
      </>}
    </main>

    <div className="actions"><button disabled={current?.status === 'inactive'} onClick={() => setModal('location')}><Plus size={20}/> Add location</button><button className="primary" disabled={!currentId || current?.status === 'inactive'} onClick={() => setModal('item')}><Plus size={20}/> Add item</button></div>
    {modal === 'location' && <LocationForm parentName={current?.name ?? 'Your Home'} onCancel={() => setModal(null)} onSubmit={addLocation}/>} 
    {modal === 'editLocation' && current && <LocationForm parentName={locationPath(current.parentId, locations)} initial={current} onCancel={() => setModal(null)} onSubmit={editLocation}/>} 
    {modal === 'item' && current && <ItemForm locationName={current.name} onCancel={() => setModal(null)} onSubmit={addItem}/>} 
    {modal === 'editItem' && selectedItem && <ItemForm locationName={locationPath(selectedItem.locationId, locations)} initial={selectedItem} onCancel={() => { setSelectedItem(null); setModal(null); }} onSubmit={editItem}/>} 
    {modal === 'moveItem' && selectedItem && <MoveForm title={`Move ${selectedItem.name}`} subtitle={`Currently in ${locationPath(selectedItem.locationId, locations)}`} choices={locations.filter((location) => location.id !== selectedItem.locationId && isAvailableLocation(location.id, locations))} allLocations={locations} onCancel={() => { setSelectedItem(null); setModal(null); }} onSubmit={moveItem}/>} 
    {modal === 'moveLocation' && current && <MoveForm title={`Move ${current.name}`} subtitle={`Currently in ${locationPath(current.parentId, locations)}`} choices={locations.filter((location) => isAvailableLocation(location.id, locations) && location.id !== current.id && !isDescendantOf(location.id, current.id, locations))} allLocations={locations} onCancel={() => setModal(null)} onSubmit={moveLocation}/>} 
    {modal === 'removeLocation' && current && <RemoveLocationForm location={current} impact={deletionImpact} onCancel={() => setModal(null)} onArchive={archiveLocation} onDelete={deleteLocation}/>} 
    {modal === 'history' && <HistoryModal title={historyTitle} entries={history} locations={locations} onCancel={closeHistory}/>} 
    {modal === 'photos' && photoTarget && <PhotoModal target={photoTarget} photos={photos} onCancel={closePhotos} onUpload={uploadPhoto} onUpdate={updatePhoto} onDelete={deletePhoto}/>} 
  </div>;
}

function locationPath(id: string | null, locations: Location[]): string {
  if (!id) return 'Home';
  const names: string[] = [];
  let cursor = locations.find((location) => location.id === id);
  while (cursor) { names.unshift(cursor.name); cursor = locations.find((location) => location.id === cursor?.parentId); }
  return names.join(' › ') || 'Unknown location';
}

function isDescendantOf(candidateId: string, ancestorId: string, locations: Location[]): boolean {
  let cursor = locations.find((location) => location.id === candidateId);
  while (cursor?.parentId) {
    if (cursor.parentId === ancestorId) return true;
    cursor = locations.find((location) => location.id === cursor?.parentId);
  }
  return false;
}

function isAvailableLocation(id: string, locations: Location[]): boolean {
  let cursor = locations.find((location) => location.id === id);
  while (cursor) {
    if (cursor.status === 'inactive') return false;
    cursor = locations.find((location) => location.id === cursor?.parentId);
  }
  return true;
}

function SearchResults({ items, matches, locations, onOpen }: { items: InventoryItem[]; matches: Location[]; locations: Location[]; onOpen: (id: string) => void }) {
  const count = items.length + matches.length;
  return <section className="search-results">
    <div className="section-heading"><div><h2>Search results</h2><p>{count ? `${count} result${count === 1 ? '' : 's'} found.` : 'Nothing matched. Try another name or note.'}</p></div></div>
    {matches.length > 0 && <section aria-label="Matching locations"><h3>Locations <span>{matches.length}</span></h3><div className="cards">
      {matches.map((location) => <button className={`location-card ${!isAvailableLocation(location.id, locations) ? 'archived-card' : ''}`} key={location.id} onClick={() => onOpen(location.id)}>
        <span className="icon"><MapPin size={22}/></span><span><strong>{location.name}</strong><small>{locationPath(location.id, locations)}{!isAvailableLocation(location.id, locations) ? ' · Archived' : ''}</small></span><ChevronRight size={21}/>
      </button>)}
    </div></section>}
    {items.length > 0 && <section aria-label="Matching items"><h3>Items <span>{items.length}</span></h3><div className="cards">
      {items.map((item) => <button className={`location-card ${!isAvailableLocation(item.locationId, locations) ? 'archived-card' : ''}`} key={item.id} onClick={() => onOpen(item.locationId)}>
        <span className="icon"><Box size={22}/></span><span><strong>{item.name}</strong><small>{locationPath(item.locationId, locations)} · Qty {item.quantity}{!isAvailableLocation(item.locationId, locations) ? ' · Archived' : ''}</small></span><ChevronRight size={21}/>
      </button>)}
    </div></section>}
  </section>;
}

function ModalFrame({ title, subtitle, onCancel, children }: { title: string; subtitle: string; onCancel: () => void; children: React.ReactNode }) {
  return <div className="modal-backdrop" onMouseDown={onCancel}><section className="modal" onMouseDown={(e) => e.stopPropagation()}><button className="close" onClick={onCancel}><X/></button><h2>{title}</h2><p>{subtitle}</p>{children}</section></div>;
}

function LocationForm({ parentName, initial, onCancel, onSubmit }: { parentName: string; initial?: Location; onCancel: () => void; onSubmit: (name: string, type: Location['type'], status: LocationStatus, note: string) => void }) {
  const [name, setName] = useState(initial?.name ?? ''); const [type, setType] = useState<Location['type']>(initial?.type ?? 'storage'); const [status, setStatus] = useState<LocationStatus>(initial?.status ?? 'active'); const [note, setNote] = useState(initial?.note ?? '');
  return <ModalFrame title={initial ? 'Edit location' : 'Add a location'} subtitle={`Inside ${parentName}`} onCancel={onCancel}><form onSubmit={(e) => { e.preventDefault(); onSubmit(name.trim(), type, status, note.trim()); }}><label>Name<input required autoFocus value={name} onChange={(e) => setName(e.target.value)} placeholder="Cabinet, shelf, suspicious pile…"/></label><div className="form-row"><label>Type<select value={type} onChange={(e) => setType(e.target.value as Location['type'])}><option value="floor">Floor</option><option value="area">Area</option><option value="room">Room</option><option value="storage">Storage</option></select></label><label>Status<select value={status} onChange={(e) => setStatus(e.target.value as LocationStatus)}><option value="active">Active</option><option value="planned">Planned</option><option value="temporary">Temporary</option><option value="inactive">Inactive</option></select></label></div><label>Notes<textarea value={note} onChange={(e) => setNote(e.target.value)} placeholder="Anything useful to remember"/></label><button className="submit">{initial ? 'Save changes' : 'Add location'}</button></form></ModalFrame>;
}

function ItemForm({ locationName, initial, onCancel, onSubmit }: { locationName: string; initial?: InventoryItem; onCancel: () => void; onSubmit: (name: string, quantity: number, note: string) => void }) {
  const [name, setName] = useState(initial?.name ?? ''); const [quantity, setQuantity] = useState(initial?.quantity ?? 1); const [note, setNote] = useState(initial?.note ?? '');
  return <ModalFrame title={initial ? 'Edit item' : 'Add an item'} subtitle={`Stored in ${locationName}`} onCancel={onCancel}><form onSubmit={(e) => { e.preventDefault(); onSubmit(name.trim(), quantity, note.trim()); }}><label>Item name<input required autoFocus value={name} onChange={(e) => setName(e.target.value)} placeholder="What are we trying not to lose?"/></label><label>Quantity<input type="number" min="1" value={quantity} onChange={(e) => setQuantity(Number(e.target.value))}/></label><label>Notes<textarea value={note} onChange={(e) => setNote(e.target.value)} placeholder="Color, condition, model, etc."/></label><button className="submit">{initial ? 'Save changes' : 'Add item'}</button></form></ModalFrame>;
}

function RemoveLocationForm({ location, impact, onCancel, onArchive, onDelete }: { location: Location; impact: LocationDeletionImpact | null; onCancel: () => void; onArchive: () => void; onDelete: () => void }) {
  const [confirmation, setConfirmation] = useState('');
  const matches = confirmation === location.name;
  return <ModalFrame title={`Remove ${location.name}`} subtitle="Choose a reversible archive or a permanent deletion." onCancel={onCancel}>
    <div className="removal-options">
      <section className="archive-option"><Archive size={25}/><div><h3>Archive location</h3><p>Hides it from normal browsing while preserving its contents, identity, and history. It can be restored later.</p></div><button type="button" onClick={onArchive}>Archive location</button></section>
      <section className="delete-option"><Trash2 size={25}/><div><h3>Delete permanently</h3><p>This cannot be undone. Permanent deletion is permitted only for an unused location with no contents or history.</p></div>
        {!impact ? <p className="impact-loading">Checking deletion safety…</p> : <>
          {!impact.canDelete && <ul className="impact-list">{impact.isRoot && <li>Root locations cannot be deleted.</li>}{impact.childLocationCount > 0 && <li>Contains {impact.childLocationCount} child location{impact.childLocationCount === 1 ? '' : 's'}.</li>}{impact.itemCount > 0 && <li>Contains {impact.itemCount} item{impact.itemCount === 1 ? '' : 's'}.</li>}{impact.photoCount > 0 && <li>Contains {impact.photoCount} photo{impact.photoCount === 1 ? '' : 's'}.</li>}{impact.movementReferenceCount > 0 && <li>Appears in {impact.movementReferenceCount} movement record{impact.movementReferenceCount === 1 ? '' : 's'}.</li>}</ul>}
          {impact.canDelete && <label>Type <strong>{location.name}</strong> to confirm<input value={confirmation} onChange={(e) => setConfirmation(e.target.value)} autoComplete="off"/></label>}
        </>}
        <button type="button" disabled={!impact?.canDelete || !matches} onClick={onDelete}>Delete permanently</button>
      </section>
    </div>
  </ModalFrame>;
}

function MoveForm({ title, subtitle, choices, allLocations, onCancel, onSubmit }: { title: string; subtitle: string; choices: Location[]; allLocations: Location[]; onCancel: () => void; onSubmit: (destinationId: string) => void }) {
  const [destinationId, setDestinationId] = useState('');
  const sorted = [...choices].sort((a, b) => locationPath(a.id, allLocations).localeCompare(locationPath(b.id, allLocations)));
  return <ModalFrame title={title} subtitle={subtitle} onCancel={onCancel}><form onSubmit={(e) => { e.preventDefault(); onSubmit(destinationId); }}><label>New location<select required autoFocus value={destinationId} onChange={(e) => setDestinationId(e.target.value)}><option value="">Choose a destination…</option>{sorted.map((location) => <option key={location.id} value={location.id}>{locationPath(location.id, allLocations)}</option>)}</select></label><button className="submit" disabled={!destinationId}>Move</button></form></ModalFrame>;
}

function HistoryModal({ title, entries, locations, onCancel }: { title: string; entries: MovementHistoryEntry[] | null; locations: Location[]; onCancel: () => void }) {
  return <ModalFrame title={`Movement history: ${title}`} subtitle="Newest moves appear first." onCancel={onCancel}>
    {entries === null ? <div className="history-loading"><Clock3 size={24}/> Retrieving the chronicles…</div> : entries.length === 0 ? <div className="history-empty"><History size={30}/><strong>No movement recorded</strong><span>This has remained steadfastly where it was put.</span></div> : <ol className="history-list">
      {entries.map((entry) => <li key={entry.id}><span className="history-marker"><MoveRight size={18}/></span><div><time dateTime={entry.movedAt}>{formatMovementDate(entry.movedAt)}</time><p><strong>{locationPath(entry.fromLocationId, locations) || entry.fromLocationName}</strong><MoveRight size={16}/><strong>{locationPath(entry.toLocationId, locations) || entry.toLocationName}</strong></p></div></li>)}
    </ol>}
  </ModalFrame>;
}

function formatMovementDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function PhotoModal({ target, photos, onCancel, onUpload, onUpdate, onDelete }: { target: PhotoTarget; photos: InventoryPhoto[] | null; onCancel: () => void; onUpload: (file: File, caption: string) => Promise<void>; onUpdate: (photo: InventoryPhoto, caption: string) => Promise<void>; onDelete: (photo: InventoryPhoto) => Promise<void> }) {
  const [file, setFile] = useState<File | null>(null); const [caption, setCaption] = useState(''); const [busy, setBusy] = useState(false); const [localError, setLocalError] = useState('');
  async function submit(e: React.FormEvent) { e.preventDefault(); if (!file) return; setBusy(true); setLocalError(''); try { await onUpload(file, caption.trim()); setFile(null); setCaption(''); const input = document.getElementById('photo-file') as HTMLInputElement | null; if (input) input.value = ''; } catch (reason) { setLocalError(reason instanceof Error ? reason.message : 'Could not upload the photo.'); } finally { setBusy(false); } }
  return <ModalFrame title={`Photos: ${target.name}`} subtitle={`Add photos to this ${target.kind}.`} onCancel={onCancel}>
    <form className="photo-upload" onSubmit={submit}><label className="camera-picker"><Camera size={24}/><span>{file ? file.name : 'Take or choose a photo'}</span><input id="photo-file" type="file" accept="image/jpeg,image/png,image/webp,image/heic,image/heif" capture="environment" onChange={(e) => setFile(e.target.files?.[0] ?? null)}/></label><label>Caption (optional)<input value={caption} onChange={(e) => setCaption(e.target.value)} placeholder="What are we looking at?"/></label>{localError && <p className="photo-error">{localError}</p>}<button className="submit" disabled={!file || busy}><Upload size={18}/>{busy ? 'Uploading…' : 'Add photo'}</button></form>
    {photos === null ? <div className="history-loading"><ImageIcon size={24}/> Loading photos…</div> : photos.length === 0 ? <div className="history-empty"><ImageIcon size={30}/><strong>No photos yet</strong><span>The photographic evidence begins here.</span></div> : <div className="photo-grid">{photos.map((photo) => <PhotoCard key={photo.id} photo={photo} onUpdate={onUpdate} onDelete={onDelete}/>)}</div>}
  </ModalFrame>;
}

function PhotoCard({ photo, onUpdate, onDelete }: { photo: InventoryPhoto; onUpdate: (photo: InventoryPhoto, caption: string) => Promise<void>; onDelete: (photo: InventoryPhoto) => Promise<void> }) {
  const [editing, setEditing] = useState(false); const [caption, setCaption] = useState(photo.caption ?? '');
  const url = store.photoUrl(photo.contentUrl);
  return <article className="photo-card"><a href={url} target="_blank" rel="noreferrer" title="Open full-size photo"><img src={url} alt={photo.caption || photo.originalFileName}/><span><ExternalLink size={15}/> Full size</span></a>{editing ? <form onSubmit={async (e) => { e.preventDefault(); await onUpdate(photo, caption.trim()); setEditing(false); }}><input value={caption} onChange={(e) => setCaption(e.target.value)} autoFocus/><button className="small-save">Save</button></form> : <p>{photo.caption || <em>No caption</em>}</p>}<small>{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(photo.createdAt))}</small><div className="photo-actions"><button onClick={() => setEditing((value) => !value)}><Pencil size={17}/> {editing ? 'Cancel' : 'Caption'}</button><button className="delete-photo" onClick={() => onDelete(photo)}><Trash2 size={17}/> Delete</button></div></article>;
}
