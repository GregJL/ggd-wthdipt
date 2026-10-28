# Gmail password recovery

Recovery is available through **Forgot password?** and **Account → Reset a forgotten
password**. It emails an eight-digit code only to the saved account address. Codes
expire after ten minutes, allow five incorrect attempts, and are consumed on reset.
There is a one-minute resend cooldown; requesting a later code replaces the old one.

## Configure locally

Enable Google 2-Step Verification and create an app password for the sending account:
https://myaccount.google.com/apppasswords

Google help: https://support.google.com/accounts/answer/185833

Copy the 16-character app password, then run from the project directory:

```powershell
git pull
.\stop-background-ggd.cmd
powershell -NoProfile -ExecutionPolicy Bypass -File .\configure-recovery-email.ps1 -FromClipboard
dotnet ef database update --project server
.\publish-ggd.cmd
.\start-background-ggd.cmd
```

The script asks for the sending Gmail/Google Workspace address, clears the clipboard,
and saves the sending credentials in .NET User Secrets. Restart to load configuration.
The sending address can differ from the receiving account address. Use the new Google
app password rather than the Google account password. Managed accounts may restrict
app passwords; changing the Google password revokes app passwords.

Reload the HTTPS app, request a code, enter it with a new password, then sign in and
save the new password in a password manager. Setup does not require the forgotten
application password. Account IDs, membership, and inventory remain unchanged.

## Implementation and checks

The migration adds PasswordRecoveries. It stores a Data Protection-encrypted hash
and security-stamp snapshot, never plaintext recovery codes or reusable reset tokens.
Password changes invalidate outstanding codes. Resetting clears login lockout and
invalidates old sign-in sessions on their next request.

Gmail delivery uses a bounded in-memory queue with mandatory STARTTLS on port 587.
Restarting the server can discard queued mail; request another code after a minute
if necessary. Check spam and sanitized recovery-delivery warnings if mail does not
arrive. Code requests return generic responses and have rate/antiforgery checks.

Run `dotnet run --project tests/Ggd.AuthTests` for real endpoint checks against a
disposable database and fake sender. These cover saved-address delivery, generic
responses, cooldown, expiry, attempt limits, password validation, replay prevention,
session invalidation, and data preservation. Verify actual Gmail delivery locally
after configuration. Credentials, codes, and account data do not belong in Git.
