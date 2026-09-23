# GGD-WTHDIPT

**Greg's Giant Database of Where the Hell Did I Put That?** is a mobile-first household inventory app.

This milestone provides a working React/TypeScript client connected to a .NET 10 ASP.NET Core + EF Core 10 + PostgreSQL API. It includes the seeded home hierarchy, item and location editing, independent edit and move locks, safe archiving and deletion, movement history, phone access over a private home network, and item/location photos stored on disk with metadata in PostgreSQL.

## Run the client

```bash
cd client
npm install
npm run dev
```

Open the displayed local URL on a phone on the same network by starting Vite with `npm run dev -- --host`.

## Run the API (requires .NET 10 SDK and PostgreSQL 18)

1. Create the development database from PowerShell:

```powershell
createdb -U postgres ggd_inventory
```

2. Store the local connection string—including your real PostgreSQL password—in .NET user secrets. From `server/`, run:

```powershell
dotnet user-secrets set "ConnectionStrings:Inventory" "Host=localhost;Port=5432;Database=ggd_inventory;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
```

Do not put the password in `appsettings.json` or commit it to source control.

3. Restore packages, create the database schema, and start the API:

```bash
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

When the API starts in the Development environment, it seeds Greg's workspace and the known home-location hierarchy if they are not already present. The seeder is idempotent: restarting the API does not create duplicates.

## Run the connected React client

Keep the API running, then open a second terminal:

```bash
cd client
npm install
npm run dev
```

The client derives the API host from the address used to open the page. On the PC it calls `localhost:5000`; on a phone opened with the PC's private IP address it calls that same private IP on port 5000. `client/.env` can still override the API address when needed.

Items can be added and deleted from the mobile interface. Item deletion requires confirmation and is scoped to the current workspace by the API.

Items and locations can also be edited without changing their identity or placement. Item edits cover name, quantity, and notes. Location edits cover name, type, status, and notes.

Edit locks are independent of move locks. An edit-locked record can still be moved unless its move lock is also enabled, and a move-locked record can still be edited unless its edit lock is enabled. Both protections are enforced by the API.

Locations can be archived (mapped to the `Inactive` status), restored, or permanently deleted. Archived locations are hidden from ordinary browsing and excluded—along with their descendants—from add and move destinations, while their contents remain searchable. Permanent deletion requires an exact-name confirmation and is allowed only for a non-root location with no children, items, or movement-history references.

## Movement and locking

Items and non-root locations can be moved without changing their identities. Every move creates an `ItemMovement` or `LocationMovement` history record in the same database transaction. A Movement history control on each item and location opens a newest-first timeline showing when it moved and its old and new locations. Items and locations may be move-locked; the API enforces the lock even when called outside the React interface. Root locations cannot be moved, and location moves are checked for cross-workspace destinations and containment cycles.

## Phone access and photo support

Photo metadata is stored in PostgreSQL. Image files are stored by default in:

```text
D:\ggd-wthdipt-data\photos
```

The API creates that directory automatically on the first upload. To choose another folder, run this from `server` before starting the API:

```powershell
dotnet user-secrets set "PhotoStorage:Path" "D:\your-preferred-photo-folder"
```

The default maximum photo size is 15 MB. JPEG, PNG, WebP, HEIC, and HEIF uploads are accepted. HEIC/HEIF display depends on browser support; JPEG is the most universally viewable format.

To install this feature over the previous version, replace these files:

```text
client/src/App.tsx
client/src/data/store.ts
client/src/styles.css
client/src/types.ts
client/package.json
server/Contracts/Responses.cs
server/Contracts/Requests.cs
server/Data/InventoryDbContext.cs
server/Models/Entities.cs
server/Program.cs
server/appsettings.json
```

Preserve the existing `server/Migrations` folder, then create and apply the new schema migration:

```powershell
cd D:\ggd-wthdipt\server
dotnet ef migrations add AddPhotoSupport
dotnet ef database update
dotnet run
```

In the client terminal:

```powershell
cd D:\ggd-wthdipt\client
npm run dev
```

No `npm install` is needed because photo support uses browser and .NET features already present.

To open the app from a phone:

1. Keep the PC and phone on the same home Wi-Fi network.
2. In PowerShell on the PC, run `ipconfig` and find the active Wi-Fi adapter's **IPv4 Address** (usually `192.168.x.x`).
3. Leave both development servers running.
4. On the phone, open `http://YOUR-PC-IP:5173`—for example, `http://192.168.1.42:5173`.
5. If Windows Defender Firewall asks, allow access on **Private networks** only. Do not enable Public-network access.

The API accepts development-client requests only from `localhost` or private-network IP addresses on port 5173. This is development access for the trusted home network, not internet hosting. There is no authentication yet, so do not expose ports 5000 or 5173 through the router.

## Progressive Web App

The client includes a PWA manifest, standard and maskable icons, standalone-display metadata, and a service worker. The service worker caches the application shell but never caches API requests. If the network is unavailable, the interface clearly reports that inventory changes and photo operations require the home server. When a newer service worker is ready, the interface offers a reload button.

Browsers require a secure context for service workers and full PWA installation. `localhost` is a special exception, but a phone using a private IP address is not. The existing HTTP workflow remains available for ordinary development; installing the app on the phone requires a certificate trusted by both the PC and phone.

The Vite configuration enables HTTPS automatically when these environment variables contain a PEM certificate and key:

```powershell
$env:GGD_HTTPS_CERT_PATH="D:\ggd-wthdipt\certs\ggd-local.pem"
$env:GGD_HTTPS_KEY_PATH="D:\ggd-wthdipt\certs\ggd-local-key.pem"
npm run dev
```

The .NET API can use the same certificate on secure port 5001:

```powershell
$env:ASPNETCORE_Kestrel__Certificates__Default__Path="D:\ggd-wthdipt\certs\ggd-local.pem"
$env:ASPNETCORE_Kestrel__Certificates__Default__KeyPath="D:\ggd-wthdipt\certs\ggd-local-key.pem"
dotnet run --urls "https://0.0.0.0:5001"
```

The client automatically uses port 5000 when opened over HTTP and port 5001 when opened over HTTPS. Certificate files belong in the ignored `certs` directory and must never be committed. When using a local certificate authority such as mkcert, install only its public root certificate on the phone—never copy or share the root private key.

Once the certificate authority is trusted on the phone, open `https://YOUR-PC-IP:5173` and use the browser's **Install app** or **Add to Home Screen** action. The exact certificate-trust and installation steps differ between Android and iPhone.

This PWA update changes or adds:

```text
.gitignore
client/index.html
client/vite.config.ts
client/public/manifest.webmanifest
client/public/sw.js
client/public/icons/*
client/src/App.tsx
client/src/data/store.ts
client/src/main.tsx
client/src/pwa.ts
client/src/styles.css
```

It requires no database migration and no new npm package.

## Single published application

The Release publish process now builds the React client and places it in the ASP.NET application's `wwwroot` directory. ASP.NET serves the PWA, API, and stored-photo responses from one HTTPS origin. Vite remains available for development but is not required to run the published application.

The published application explicitly loads the existing `ggd-wthdipt-development` user-secrets store, so the PostgreSQL password does not need to be copied into `appsettings.json`. This local deployment must run under the same Windows account that created the user secret.

To publish:

```powershell
cd D:\ggd-wthdipt
.\publish-ggd.cmd
```

That command runs `npm ci`, builds the React production client, publishes the .NET Release application, and creates:

```text
D:\ggd-wthdipt\publish
```

Stop both development servers, then start the single application:

```powershell
cd D:\ggd-wthdipt
.\start-ggd.cmd
```

Open it on the PC at:

```text
https://localhost:5001
```

Open it on the phone at:

```text
https://192.168.68.108:5001
```

Because the installed PWA's origin includes its port, remove the old port-5173 home-screen installation and add the port-5001 page to the home screen. After that, ordinary use requires only `start-ggd.cmd`; neither VS Code nor the Vite development server is needed.

This deployment change adds or modifies:

```text
client/src/data/store.ts
server/Ggd.Api.csproj
server/Program.cs
publish-ggd.cmd
start-ggd.cmd
```

It requires no database migration. The existing PostgreSQL database, user-secret connection string, certificates, and `D:\ggd-wthdipt-data\photos` folder remain unchanged.
