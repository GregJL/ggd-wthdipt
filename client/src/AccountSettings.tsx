import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { auth } from './data/auth';
import type { AccountUser } from './types';

export default function AccountSettings({ account, onSignOut, onClose, onRecover }: { account: AccountUser; onSignOut: () => Promise<void>; onClose: () => void; onRecover: () => void }) {
  const panel = useRef<HTMLElement>(null);
  const previousFocus = useRef(document.activeElement as HTMLElement | null);
  useEffect(() => {
    function keydown(event: KeyboardEvent) {
      if (event.key === 'Escape') { onClose(); return; }
      if (event.key !== 'Tab') return;
      const fields = [...(panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not([hidden]):not(:disabled)') ?? [])];
      const first = fields[0]; const last = fields.at(-1);
      if (event.shiftKey && document.activeElement === first && last) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last && first) { event.preventDefault(); first.focus(); }
    }
    document.addEventListener('keydown', keydown);
    return () => { document.removeEventListener('keydown', keydown); previousFocus.current?.focus(); };
  }, []);
  const [current, setCurrent] = useState(''); const [password, setPassword] = useState(''); const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false); const [message, setMessage] = useState('');
  return <div className="modal-backdrop"><section ref={panel} className="modal account-card" role="dialog" aria-modal="true" aria-labelledby="account-title">
    <button className="close" onClick={onClose} aria-label="Close account" autoFocus><X/></button><h2 id="account-title">Your account</h2><p>{account.displayName} · {account.email}</p>
    <form onSubmit={async (event) => {
      event.preventDefault(); setMessage('');
      if (password !== confirm) { setMessage('The new passwords do not match.'); return; }
      setBusy(true);
      try { await auth.changePassword(current, password); setCurrent(''); setPassword(''); setConfirm(''); setMessage('Password changed.'); }
      catch (reason) { setMessage(reason instanceof Error ? reason.message : 'Could not change password.'); }
      finally { setBusy(false); }
    }}>
      <h3>Change password</h3><input type="text" autoComplete="username" value={account.email} hidden readOnly/>
      <label>Current password<input type="password" required autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)}/></label>
      <label>New password<input type="password" required minLength={12} maxLength={1024} autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)}/></label>
      <label>Confirm new password<input type="password" required autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)}/></label>
      <button className="submit" disabled={busy}>Change password</button>
    </form>
    <button className="search-reset account-signout" disabled={busy} onClick={onRecover}>Reset a forgotten password</button>
    {message && <p role="status">{message}</p>}
    <button className="search-reset account-signout" disabled={busy} onClick={async () => {
      setBusy(true); try { await onSignOut(); } catch { setMessage('Could not sign out. Check the connection and try again.'); setBusy(false); }
    }}>Sign out</button>
  </section></div>;
}
