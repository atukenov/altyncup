# Yurt Production Deployment Steps

This document describes step-by-step production deployment for the Yurt project as it exists in this repository.
It covers the backend API, the Angular admin frontend, and the Capacitor-based customer mobile app.

## 1. Prerequisites

Install the following before deploying:

- .NET 8 SDK / runtime
- SQL Server, SQL Server Express, or managed SQL Server instance
- Node.js 18+ and npm 10+
- Angular CLI 21 (`npm install -g @angular/cli@21`)
- Java JDK 17+ and Android Studio (for Android mobile builds)
- Xcode + Apple Developer account (only if you plan to build iOS)

## 2. Project-specific notes

The repository contains:

- `backend/` — ASP.NET Core 8 API in `src/Yurt.WebApi/`
- `frontend/` — Angular 21 workspace with `yurt-admin` and `yurt-customer`
- `frontend/capacitor.config.ts` — Capacitor config pointing at `dist/yurt-customer/browser`
- `backend/src/Yurt.WebApi/appsettings.json` — production settings template
- `frontend/projects/yurt-admin/src/environments/environment.ts` and `frontend/projects/yurt-customer/src/environments/environment.ts`

Important current behavior:

- Both Angular apps use the same `environment.ts` file for `apiUrl`.
- The Capacitor mobile wrapper is configured to load `dist/yurt-customer/browser`.
- Backend CORS is driven by `AllowedOrigins` in `appsettings.json`.

## 3. Backend production deployment

### 3.1 Configure production values

Edit `backend/src/Yurt.WebApi/appsettings.json` or use environment variables for production values.

At minimum, set:

- `ConnectionStrings:DefaultConnection` to your production SQL Server connection string
- `Jwt:Secret` to a secure random secret
- `AllowedOrigins` to include your deployed frontend / app origins
- `AllowedHosts` to your production domain or keep `*` if necessary

Example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-sql-server;Database=YurtDb;User Id=appuser;Password=StrongPassword123!;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "VeryStrongProductionJwtSecretHere",
    "Issuer": "yurt-api",
    "Audience": "yurt-clients",
    "ExpiryDays": "7"
  },
  "AllowedOrigins": ["https://admin.yourt-domain.com", "capacitor://localhost"],
  "AllowedHosts": "*"
}
```

If you prefer environment variables, use the ASP.NET Core convention:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Secret`
- `AllowedOrigins__0`, `AllowedOrigins__1`, etc.
- `ASPNETCORE_ENVIRONMENT=Production`

### 3.2 Apply database migrations

From repository root:

```bash
cd backend
dotnet restore
dotnet tool restore
dotnet ef database update --project src/Yurt.Infrastructure --startup-project src/Yurt.WebApi
```

If you host the backend on a server, you can also run migrations there before startup.

### 3.3 Publish the backend

From the repository root:

```bash
cd backend
dotnet publish src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish
```

### 3.4 Deploy the published API

#### Option A: Linux server + Nginx

1. Copy published files to the server, for example `/var/www/yurt-api`.
2. Create a `systemd` service at `/etc/systemd/system/yurt-api.service`:

```ini
[Unit]
Description=Yurt API
After=network.target

[Service]
WorkingDirectory=/var/www/yurt-api
ExecStart=/usr/bin/dotnet /var/www/yurt-api/Yurt.WebApi.dll
Restart=always
RestartSec=10
SyslogIdentifier=yurt-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

3. Enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable yurt-api
sudo systemctl start yurt-api
```

4. Configure Nginx as a reverse proxy:

```nginx
server {
  listen 80;
  server_name api.yourt-domain.com;

  location / {
    proxy_pass http://127.0.0.1:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection keep-alive;
    proxy_set_header Host $host;
    proxy_cache_bypass $http_upgrade;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
  }
}
```

5. Restart Nginx:

```bash
sudo systemctl restart nginx
```

6. Add HTTPS with Let’s Encrypt using Certbot.

#### Option B: Managed host (Railway)

Railway is a convenient managed host for your .NET backend.

1. Create a new Railway project and add a service.
2. Connect your GitHub repository or use the Railway CLI.
3. Set the build command to publish the API in Release mode, for example:

```bash
dotnet publish backend/src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish
```

4. Set the start command to run the published API:

```bash
dotnet backend/src/Yurt.WebApi/publish/Yurt.WebApi.dll
```

5. Configure Railway environment variables for production values instead of storing them in source:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Secret`
- `AllowedOrigins__0`, `AllowedOrigins__1`, etc.
- `ASPNETCORE_ENVIRONMENT=Production`

6. Expose your service with Railway’s HTTPS endpoint and note the production URL.

7. If you prefer Docker, add a simple `Dockerfile` at repo root or in `backend/`, and let Railway build the container.

8. Confirm Railway routes traffic to the app and that SSL is active.

### 3.5 Verify production backend

- Confirm the API responds on your production URL.
- Check `https://api.yourt-domain.com/swagger` if Swagger is enabled in production or use a browser/client.
- Verify the backend can connect to the production database.

## 4. Admin frontend production deployment

### 4.1 Update admin API URL

Since `frontend/projects/yurt-admin/src/environments/environment.ts` currently defines `apiUrl`, update it before production build.

Example:

```ts
export const environment = {
  production: true,
  apiUrl: "https://api.yourt-domain.com",
};
```

If you want build-time environment separation, add a production environment file and update `angular.json` to replace it.

### 4.2 Build the admin app for production

```bash
cd frontend
npm install
ng build yurt-admin --configuration production
```

The output will be in `frontend/dist/yurt-admin`.

### 4.3 Host the admin static app on Vercel

Deploy `frontend/dist/yurt-admin` to Vercel using either a GitHub integration or by manually uploading the static output.

Option A: Deploy from GitHub

1. Create a Vercel project and connect your repository.
2. Set the root directory to `frontend`.
3. Set the build command to:

```bash
npm install && npm run build -- --configuration production
```

4. Set the output directory to `dist/yurt-admin`.
5. Add any environment variables if you later externalize the API URL.
6. Assign your custom domain and enable HTTPS.

Option B: Deploy static files directly

1. Build locally:

```bash
cd frontend
npm install
ng build yurt-admin --configuration production
```

2. Drag `frontend/dist/yurt-admin` into Vercel’s static deployment interface.
3. Configure a custom domain and enable HTTPS.

If you use a custom domain, point it to Vercel and enable HTTPS.

### 4.4 Set backend CORS for admin

Add the admin domain to `AllowedOrigins` in the backend config.

Example:

```json
"AllowedOrigins": [
  "https://admin.yourt-domain.com",
  "capacitor://localhost"
]
```

### 4.5 Final admin checks

- Open the deployed admin site and confirm it loads assets.
- Verify login and API calls to the production backend.
- Confirm the admin origin is accepted by CORS.

## 5. Customer mobile app production deployment

### 5.1 Update customer API URL

Set the customer app backend URL in `frontend/projects/yurt-customer/src/environments/environment.ts`:

```ts
export const environment = {
  production: true,
  apiUrl: "https://api.yourt-domain.com",
};
```

This repo currently does not have a separate production environment file, so this value must be correct before building.

### 5.2 Build the customer web assets

```bash
cd frontend
ng build yurt-customer --configuration production
```

Confirm the output folder exists at `frontend/dist/yurt-customer/browser` because Capacitor is configured to use that folder.

### 5.3 Sync Capacitor assets

```bash
npx cap sync
```

If the Android or iOS platform isn’t already added, run:

```bash
npx cap add android
npx cap add ios
```

### 5.4 Build Android release

Open Android Studio:

- Open `frontend/android/` or the generated Capacitor Android project
- Sync project files
- Select a physical device or emulator
- Build a release APK / AAB via `Build > Generate Signed Bundle / APK`
- Sign the release artifact with your keystore

### 5.5 Build iOS release (macOS only)

Open Xcode:

- Open `frontend/ios/App/App.xcworkspace`
- Choose a device target or archive destination
- Update signing and provisioning profiles
- Build and archive for App Store or Ad Hoc distribution

### 5.6 Backend CORS for mobile apps

Add native wrapper origins to backend CORS:

- `capacitor://localhost`
- `http://localhost` (if local webview origin is needed)

Example:

```json
"AllowedOrigins": [
  "https://admin.yourt-domain.com",
  "capacitor://localhost"
]
```

### 5.7 Production distribution options

Since you already have Android and iOS developer accounts, publish to the stores:

- Google Play
  - Generate a signed AAB in Android Studio
  - Create an app in Google Play Console
  - Upload the AAB, configure store listing, privacy policy, and content rating
  - Use internal testing first, then promote to production

- Apple App Store
  - Archive the app in Xcode and upload via App Store Connect
  - Set up App Store metadata, provisioning, and App Review information
  - Test with TestFlight before releasing to production

If you want a fallback distribution option, you can still side-load an Android APK directly, but store distribution is recommended for production.

## 6. Recommended production checklist

- [ ] Confirm `Jwt:Secret` is strong and not checked into source control
- [ ] Confirm production DB connection string is correct
- [ ] Apply EF Core migrations in production environment
- [ ] Update `apiUrl` in both Angular environment files
- [ ] Add deployed origins to backend `AllowedOrigins`
- [ ] Build and deploy backend in Release mode
- [ ] Build and deploy `frontend/dist/yurt-admin`
- [ ] Build and package Android via Capacitor
- [ ] Enable HTTPS on backend and admin web host
- [ ] Test API, admin app, and mobile app against production backend

## 7. Notes and production cautions

- Because `yurt-admin` and `yurt-customer` currently share a single `environment.ts` file, any production `apiUrl` change must be managed carefully.
- If you need separate dev and prod deployment flows, add `environment.prod.ts` files and configure replacements in `angular.json`.
- `backend/src/Yurt.WebApi/Program.cs` seeds data at startup via `DbSeeder.SeedAsync`. Ensure this is acceptable before deploying to a production database.
- Keep secrets out of source control by using host-provided environment variables wherever possible.
- For Android mobile use, a production backend URL is required; `localhost` will not work in a released Capacitor app.
