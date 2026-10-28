import { useEffect, useState } from 'react';
import App from './App';
import { auth } from './data/auth';
import type { AccountSession } from './types';

export default function AccountGate() {
  const [session, setSession] = useState<AccountSession | null>(null);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let cancelled = false;
    auth.session().then((result) => { if (!cancelled) { setSession(result); setError(''); } })
      .catch(() => { if (!cancelled) setError('Could not reach your home server. Check the connection, then retry.'); });
    const expired = () => { setSession(null); setAttempt((value) => value + 1); };
    window.addEventListener('ggd-auth-expired', expired);
    return () => { cancelled = true; window.removeEventListener('ggd-auth-expired', expired); };
  }, [attempt]);
  async function signedIn() { setSession(null); setAttempt((value) => value + 1); }
  async function signOut() { await auth.logout(); setSession(null); setAttempt((value) => value + 1); }
  if (session?.user) return <App account={session.user} onSignOut={signOut}/>;
  return <div className="app-shell account-shell"><header><div className="eyebrow">Greg's giant database of</div><h1>Where the hell did I put that?</h1></header><main>
    {!session ? <section className="account-card">{error ? <><p role="alert">{error}</p><button onClick={() => setAttempt((value) => value + 1)}>Retry</button></> : <p role="status">Connecting to your household…</p>}</section>
      : <SignIn session={session} onSignedIn={signedIn}/>}
  </main></div>;
}

function SignIn({ session, onSignedIn }: { session: AccountSession; onSignedIn: () => Promise<void> }) {
  const [email, setEmail] = useState(''); const [displayName, setDisplayName] = useState('Greg');
  const [password, setPassword] = useState(''); const [confirm, setConfirm] = useState('');
  const [code, setCode] = useState(''); const [rememberMe, setRememberMe] = useState(true);
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const setup = session.setupRequired;
  return <section className="account-card"><h2>{setup ? 'Set up your account' : 'Welcome back'}</h2>
    <p>{setup ? 'Your existing household and inventory will stay connected to your account.' : 'Sign in to your household inventory.'}</p>
    {setup && !session.setupAvailable ? <p>First, run <strong>configure-account-setup.ps1</strong> on the server PC, then restart the app and reload this page.</p>
      : <form onSubmit={async (event) => {
        event.preventDefault(); setError('');
        if (setup && password !== confirm) { setError('The passwords do not match.'); return; }
        setBusy(true);
        try {
          if (setup) await auth.setup(email.trim(), displayName.trim(), password, code.trim());
          else await auth.login(email.trim(), password, rememberMe);
          setPassword(''); setConfirm(''); setCode(''); await onSignedIn();
        } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not sign in.'); }
        finally { setBusy(false); }
      }}>
        {setup && <label>Display name<input required maxLength={100} autoComplete="nickname" value={displayName} onChange={(e) => setDisplayName(e.target.value)}/></label>}
        <label>Email<input type="email" required maxLength={256} autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)}/></label>
        <label>Password<input type="password" required minLength={setup ? 12 : undefined} maxLength={1024} autoComplete={setup ? 'new-password' : 'current-password'} value={password} onChange={(e) => setPassword(e.target.value)}/></label>
        {setup ? <><p className="account-hint">Use at least 12 characters. A memorable passphrase works well.</p><label>Confirm password<input type="password" required autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)}/></label><label>One-time setup code<input required autoComplete="off" spellCheck={false} value={code} onChange={(e) => setCode(e.target.value)}/></label></>
          : <label className="account-check"><input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)}/> Keep me signed in on this device</label>}
        {error && <p role="alert">{error}</p>}<button className="submit" disabled={busy}>{busy ? 'Please wait…' : setup ? 'Create my account' : 'Sign in'}</button>
      </form>}
  </section>;
}
