import { useEffect, useMemo, useState } from 'react';
import { WORKSPACE_ID } from './data/seed';
import { store } from './data/store';
import { organizationView } from './organization';
import type { InventoryItem, Location, OrganizationTask, OrganizationTaskDraft } from './types';

export default function OrganizationBoard({ items, locations, loading, path, onClose, onOpen, onPlan, onMove, onSorted, onPhotos, onRefresh }: {
  items: InventoryItem[]; locations: Location[]; loading: boolean; path: (id: string) => string;
  onClose: () => void; onOpen: (id: string) => void; onPlan: (item: InventoryItem) => void;
  onMove: (item: InventoryItem) => Promise<void>; onSorted: (item: InventoryItem) => Promise<void>;
  onPhotos: (kind: 'item' | 'location', id: string, name: string) => void; onRefresh: () => Promise<void>;
}) {
  const [tasks, setTasks] = useState<OrganizationTask[]>([]); const [tasksLoading, setTasksLoading] = useState(true);
  const [tasksReady, setTasksReady] = useState(false);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [filter, setFilter] = useState<'attention' | 'needsSorting' | 'staged' | 'planned'>('attention');
  const [showDone, setShowDone] = useState(false); const [editor, setEditor] = useState<OrganizationTask | 'new' | null>(null);
  const view = useMemo(() => organizationView(items, locations), [items, locations]);
  useEffect(() => {
    let cancelled = false;
    store.organizationTasks(WORKSPACE_ID).then((result) => { if (!cancelled) { setTasks(result); setTasksReady(true); } })
      .catch(() => { if (!cancelled) setError('Could not load organization tasks. Use Refresh to retry.'); })
      .finally(() => { if (!cancelled) setTasksLoading(false); });
    return () => { cancelled = true; };
  }, []);
  async function run(action: () => Promise<void>) {
    setBusy(true); setError(''); try { await action(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not complete that action.'); }
    finally { setBusy(false); }
  }
  const done = tasks.filter((task) => task.isCompleted).length;
  const stagingAreas = locations.filter((location) => location.isStaging && !view.archived.has(location.id));
  return <section className="organization-board">
    <div className="section-heading"><div><h2>Help Me Fucking Organize This Shit</h2><p>Flag it. Plan a home. Move it when you actually relocate it. Mark it sorted.</p></div><button className="search-close" onClick={onClose}>Back to inventory</button></div>
    <button className="search-reset" disabled={busy} onClick={() => run(async () => { await onRefresh(); setTasks(await store.organizationTasks(WORKSPACE_ID)); setTasksLoading(false); setTasksReady(true); })}>Refresh</button>
    {error && <p className="error-banner" role="alert">{error}</p>}
    {loading ? <p role="status">Loading inventory…</p> : <div className="organization-metrics">
      <div><strong>{view.needsSorting.length}</strong><span>Need sorting</span></div><div><strong>{view.staged.length}</strong><span>In staging areas</span></div><div><strong>{view.planned.length}</strong><span>Have a destination planned</span></div>
    </div>}
    <section><h3>Staging areas</h3><p>Mark a location as a staging area in Add location or Edit location. Contents of its nested locations count as staged too.</p>
      {stagingAreas.map((location) => <div className="organization-staging" key={location.id}><button className="search-reset" onClick={() => onOpen(location.id)}>{path(location.id)}</button><button className="search-reset" onClick={() => onPhotos('location', location.id, location.name)}>Before / after photos</button></div>)}
      {!stagingAreas.length && <p>No staging areas marked yet.</p>}
    </section>
    <section><h3>Items to organize</h3><label>Show<select value={filter} onChange={(e) => setFilter(e.target.value as typeof filter)}><option value="attention">All items needing attention</option><option value="needsSorting">Flagged for sorting</option><option value="staged">In staging areas</option><option value="planned">With planned destinations</option></select></label>
      {!loading && !view[filter].length && <p>No items in this view. Use an item's Organize button to flag it or plan a destination.</p>}
      <ul className="organization-worklist">{view[filter].map((item) => {
        const plan = item.plannedLocationId; const available = plan && locations.some((location) => location.id === plan) && !view.archived.has(plan);
        return <li key={item.id}><strong>{item.name}</strong><p>Currently: {path(item.locationId)}</p>
          <p>{[item.needsSorting ? 'Needs sorting' : '', view.staging.has(item.locationId) ? 'In staging' : ''].filter(Boolean).join(' · ')}</p>
          {plan && <p>Planned: {path(plan)}{!available ? ' — unavailable; choose another destination' : plan === item.locationId ? ' — already here; mark sorted when ready' : ''}</p>}
          <div className="organization-buttons"><button disabled={busy || item.isEditLocked} onClick={() => onPlan(item)}>Edit plan</button><button onClick={() => onOpen(item.locationId)}>Open location</button><button onClick={() => onPhotos('item', item.id, item.name)}>Photos</button>
            {plan && plan !== item.locationId && <button disabled={busy || item.isMoveLocked || !available} onClick={() => run(() => onMove(item))}>Move to plan</button>}
            {(item.needsSorting || plan) && <button disabled={busy || item.isEditLocked} onClick={() => run(() => onSorted(item))}>Mark sorted / clear plan</button>}
          </div>{(item.isEditLocked || item.isMoveLocked) && <small>{item.isEditLocked ? 'Editing is locked. ' : ''}{item.isMoveLocked ? 'Movement is locked.' : ''}</small>}
        </li>;
      })}</ul>
    </section>
    <section><h3>Organization tasks</h3>
      {tasksLoading ? <p role="status">Loading tasks…</p> : !tasksReady ? <p>Task progress is unavailable until tasks load.</p> : <><p>{done} of {tasks.length} tasks completed</p><progress aria-label="Completed organization tasks" value={done} max={Math.max(tasks.length, 1)}/></>}
      <div className="organization-buttons"><button disabled={busy || editor !== null} onClick={() => setEditor('new')}>Add task</button><label className="organization-check"><input type="checkbox" checked={showDone} onChange={(e) => setShowDone(e.target.checked)}/> Show completed</label></div>
      {editor && <TaskForm key={typeof editor === 'string' ? 'new' : editor.id} initial={typeof editor === 'string' ? undefined : editor} items={items} locations={locations} path={path} onCancel={() => setEditor(null)} onSave={async (draft) => {
        setBusy(true);
        try {
          const saved = typeof editor === 'string' ? await store.addOrganizationTask(WORKSPACE_ID, draft) : await store.updateOrganizationTask(WORKSPACE_ID, editor.id, draft);
          setTasks((known) => typeof editor === 'string' ? [saved, ...known] : known.map((task) => task.id === saved.id ? saved : task)); setEditor(null);
        } finally { setBusy(false); }
      }}/>}
      <ul className="organization-worklist">{tasks.filter((task) => showDone || !task.isCompleted).map((task) => <li key={task.id}><strong>{task.isCompleted ? '✓ ' : ''}{task.title}</strong>{task.note && <p className="task-notes">{task.note}</p>}{task.referenceName && <small>Related: {task.referenceName}</small>}
        <div className="organization-buttons"><button disabled={busy || editor !== null} onClick={() => run(async () => { const saved = await store.updateOrganizationTask(WORKSPACE_ID, task.id, { ...task, isCompleted: !task.isCompleted }); setTasks((known) => known.map((candidate) => candidate.id === saved.id ? saved : candidate)); })}>{task.isCompleted ? 'Reopen' : 'Complete'}</button><button disabled={busy || editor !== null} onClick={() => setEditor(task)}>Edit</button><button disabled={busy || editor !== null} onClick={() => {
          if (window.confirm(`Delete organization task “${task.title}”?`)) void run(async () => { await store.deleteOrganizationTask(WORKSPACE_ID, task.id); setTasks((known) => known.filter((candidate) => candidate.id !== task.id)); });
        }}>Delete task</button></div>
      </li>)}</ul>
    </section>
  </section>;
}

function TaskForm({ initial, items, locations, path, onSave, onCancel }: { initial?: OrganizationTask; items: InventoryItem[]; locations: Location[]; path: (id: string) => string; onSave: (task: OrganizationTaskDraft) => Promise<void>; onCancel: () => void }) {
  const [title, setTitle] = useState(initial?.title ?? ''); const [note, setNote] = useState(initial?.note ?? '');
  const [reference, setReference] = useState(initial?.itemId ? `item:${initial.itemId}` : initial?.locationId ? `location:${initial.locationId}` : '');
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const missing = reference && ![...items.map((item) => `item:${item.id}`), ...locations.map((location) => `location:${location.id}`)].includes(reference);
  return <form className="organization-task-form" onSubmit={async (event) => {
    event.preventDefault(); setBusy(true); setError('');
    try { await onSave({ title: title.trim(), note: note.trim() || null, itemId: reference.startsWith('item:') ? reference.slice(5) : null, locationId: reference.startsWith('location:') ? reference.slice(9) : null, isCompleted: initial?.isCompleted ?? false }); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not save the task.'); }
    finally { setBusy(false); }
  }}><label>Task title<input required maxLength={200} autoFocus value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Sort the toolbox, label the shelves…"/></label><label>Notes<textarea maxLength={2000} value={note} onChange={(e) => setNote(e.target.value)}/></label>
    <label>Related item or location (optional)<select value={reference} onChange={(e) => setReference(e.target.value)}><option value="">Whole household / no link</option>{missing && <option value={reference}>{initial?.referenceName} (no longer in inventory)</option>}<optgroup label="Items">{items.map((item) => <option key={item.id} value={`item:${item.id}`}>{item.name} — {path(item.locationId)}</option>)}</optgroup><optgroup label="Locations">{locations.map((location) => <option key={location.id} value={`location:${location.id}`}>{path(location.id)}</option>)}</optgroup></select></label>
    {error && <p role="alert">{error}</p>}<div className="organization-buttons"><button className="submit" disabled={busy}>{busy ? 'Saving…' : 'Save task'}</button><button type="button" disabled={busy} onClick={onCancel}>Cancel</button></div>
  </form>;
}
