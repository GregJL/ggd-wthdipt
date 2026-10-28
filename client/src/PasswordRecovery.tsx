import { useState } from 'react';
import { auth } from './data/auth';

export default function PasswordRecovery({ available, initialEmail, onBack, onComplete }: {
  available: boolean; initialEmail: string; onBack: () => void; onComplete: () => void;
}) {
  const [email, setEmail] = useState(initialEmail); const [code, setCode] = useState('');
  const [password, setPassword] = useState(''); const [confirm, setConfirm] = useState('');
  const [requested, setRequested] = useState(false); const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState(''); const [done, setDone] = useState(false);
  async function sendCode() {
    setBusy(true); setMessage('');
    try { const result = await auth.requestRecovery(email.trim()); setRequested(true); setMessage(result.message); }
    catch (reason) { setMessage(reason instanceof Error ? reason.message : 'Could not request a code.'); }
    finally { setBusy(false); }
  }
  return <section className="account-card"><h2>Reset your password</h2>
    {!available ? <p>Recovery email needs to be configured on your server PC. Run <strong>configure-recovery-email.ps1</strong>, restart the app, then reload.</p>
      : done ? <><p>Password reset. Sign in with your new password, and save it in your password manager.</p><button className="submit" onClick={onComplete}>Back to sign-in</button></>
      : <><form onSubmit={(event) => { event.preventDefault(); void sendCode(); }}>
        <label>Account email<input type="email" required maxLength={256} autoComplete="username" value={email} onChange={(e) => { setEmail(e.target.value); setRequested(false); setCode(''); }}/></label>
        <button className="submit" disabled={busy}>{requested ? 'Send a new code' : 'Email me a recovery code'}</button>
      </form>
      {requested && <><p>The code has eight digits and expires after ten minutes. Wait a minute before requesting another code, and use the newest one.</p><form onSubmit={async (event) => {
        event.preventDefault(); setMessage('');
        if (password !== confirm) { setMessage('The passwords do not match.'); return; }
        setBusy(true);
        try { await auth.resetPassword(email.trim(), code.trim(), password); setPassword(''); setConfirm(''); setCode(''); setDone(true); }
        catch (reason) { setMessage(reason instanceof Error ? reason.message : 'Could not reset password.'); }
        finally { setBusy(false); }
      }}>
        <label>Recovery code<input required inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{8}" maxLength={8} value={code} onChange={(e) => setCode(e.target.value)}/></label>
        <label>New password<input type="password" required minLength={12} maxLength={1024} autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)}/></label>
        <label>Confirm new password<input type="password" required autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)}/></label>
        <button className="submit" disabled={busy}>Reset password</button>
      </form></>}
      {message && <p role="status">{message}</p>}</>}
    {!done && <button className="search-reset account-signout" disabled={busy} onClick={onBack}>Back</button>}
  </section>;
}
