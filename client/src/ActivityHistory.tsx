import { useEffect, useRef, useState } from 'react';
import { Clock3, RefreshCw, X } from 'lucide-react';
import { store } from './data/store';
import { WORKSPACE_ID } from './data/seed';
import type { ActivityEntry, ActivitySubject } from './types';

export default function ActivityHistory({ subject, onClose }: { subject: ActivitySubject | null; onClose: () => void }) {
  const panel = useRef<HTMLElement>(null);
  const previousFocus = useRef(document.activeElement as HTMLElement | null);
  useEffect(() => {
    function keydown(event: KeyboardEvent) {
      if (event.key === 'Escape') { onClose(); return; }
      if (event.key !== 'Tab') return;
      const focusable = [...(panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled), summary') ?? [])];
      const first = focusable[0]; const last = focusable.at(-1);
      if (event.shiftKey && document.activeElement === first && last) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last && first) { event.preventDefault(); first.focus(); }
    }
    document.addEventListener('keydown', keydown);
    return () => { document.removeEventListener('keydown', keydown); previousFocus.current?.focus(); };
  }, []);
  const [entries, setEntries] = useState<ActivityEntry[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    let cancelled = false;
    setLoading(true); setError('');
    store.activity(WORKSPACE_ID, subject).then((page) => {
      if (!cancelled) { setEntries(page.entries); setHasMore(page.hasMore); }
    }).catch(() => { if (!cancelled) setError('Could not load activity history. Try again.'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [subject, refresh]);
  async function loadOlder() {
    setLoading(true); setError('');
    try {
      const page = await store.activity(WORKSPACE_ID, subject, entries.at(-1)?.id);
      setEntries((known) => [...known, ...page.entries]); setHasMore(page.hasMore);
    } catch { setError('Could not load older activity. Try Load older again.'); }
    finally { setLoading(false); }
  }
  return <div className="modal-backdrop"><section ref={panel} className="modal activity-modal" role="dialog" aria-modal="true" aria-labelledby="activity-title">
    <button className="close" onClick={onClose} aria-label="Close activity history" autoFocus><X/></button>
    <h2 id="activity-title">Activity history</h2><p>{subject?.name ?? 'All inventory activity'}</p>
    <p className="activity-intro">New actions are recorded from this update onward. Earlier moves remain in Movement history.</p>
    <button className="search-reset" disabled={loading} onClick={() => setRefresh((value) => value + 1)}><RefreshCw size={16}/> Refresh</button>
    {error && <p role="alert">{error}</p>}
    {!entries.length && !loading && !error && <p>No activity recorded yet.</p>}
    <ol className="activity-list">
      {entries.map((entry) => <li key={entry.id}><Clock3 size={18}/><div>
        <time dateTime={entry.recordedAt}>{new Date(entry.recordedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}</time>
        <strong>{entry.summary}</strong><span className="activity-path">{entry.locationPath}</span>
        {entry.details && <details><summary>Details</summary><p>{entry.details}</p></details>}
      </div></li>)}
    </ol>
    {loading && <p role="status">Loading activity…</p>}
    {hasMore && <button className="search-reset" disabled={loading} onClick={loadOlder}>Load older</button>}
  </section></div>;
}
