# Deployment Guide

This guide explains how to deploy the Yurt project in a budget-friendly way. It covers:
- Customer app as a mobile application (Android / iOS)
- Admin app as a web app or Windows executable
- Backend deployment with low-cost hosting options

## 1. Budget-friendly strategy

Recommended budget approach:
- Host the admin app as a web app using free static hosting (Netlify, Vercel, or Azure Static Web Apps)
- Host the backend on a low-cost cloud instance or platform service
- Build the customer app as a native Android wrapper using Capacitor
- Avoid iOS App Store deployment unless you can afford Apple Developer membership ($99/year)

A realistic budget range is:
- Android store registration: $25 one-time
- Backend hosting: $5–$15 per month
- Static admin web hosting: free or included in platform free tiers

## 2. Deploy the backend

### 2.1 Option A: Cheap VPS or cloud server ($5–$15 / month)

This is the most budget-friendly long-term option.

1. Choose a provider with a small Linux VM:
   - Hetzner Cloud: $5/month
   - Vultr: $5/month
   - Oracle Cloud free tier
   - Cloudflare R2 + worker is not suitable for ASP.NET Core directly

2. Install prerequisites on the server:
   - `sudo apt update && sudo apt install -y wget apt-transport-https dotnet-runtime-8.0 nginx`
   - Follow Microsoft docs for `.NET 8` install on Ubuntu

3. Publish the API:

```bash
dotnet publish src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish
```

4. Copy the published files to the server:

```bash
scp -r backend/src/Yurt.WebApi/publish/* user@server:/var/www/yurt-api
```

5. Create a systemd service file on the server:

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

Save it as `/etc/systemd/system/yurt-api.service`, then run:

```bash
sudo systemctl daemon-reload
sudo systemctl enable yurt-api
sudo systemctl start yurt-api
```

6. Configure Nginx as a reverse proxy:

```nginx
server {
    listen 80;
    server_name your-domain.com;

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

7. Restart Nginx:

```bash
sudo systemctl restart nginx
```

### 2.2 Option B: Managed platform service (low-cost)

Use a platform with a free or low-cost tier:
- Render.com Web Service (free tier available)
- Railway.app (free tier with usage limits)
- Azure App Service Basic B1 (~$13/month)

For Render or Railway, choose a service that supports .NET apps and deploy from GitHub or directly from the repo.

### 2.3 Required backend configuration

Update `backend/src/Yurt.WebApi/appsettings.json`:
- `AllowedOrigins` must include your deployed frontend URLs
- `DefaultConnection` must point to your production database server
- `Jwt.Secret` should be a secure production secret

Use environment variables if the host supports them.

## 3. Deploy the frontend customer app as mobile

The customer app is best deployed as a Capacitor mobile wrapper.

### 3.1 Install Capacitor

From `frontend`:

```bash
cd frontend
npm install @capacitor/core @capacitor/cli @capacitor/android
```

For iOS (macOS only):

```bash
npm install @capacitor/ios
```

### 3.2 Initialize Capacitor

```bash
npx cap init "Yurt" "com.yurt.app" --web-dir=dist/yurt-customer/browser
```

### 3.3 Build the production customer app

```bash
ng build yurt-customer --configuration production
```

### 3.4 Add Android platform

```bash
npx cap add android
npx cap sync android
npx cap open android
```

### 3.5 Build and run in Android Studio

- Open the project in Android Studio
- Choose an emulator or connect a device
- Run the app
- For release builds, use **Build → Generate Signed Bundle / APK**

### 3.6 Add iOS platform (macOS only)

```bash
npx cap add ios
npx cap sync ios
npx cap open ios
```

Then open Xcode, select a target device, and run.

### 3.7 Budget notes for customer mobile

- Android side-loading is free.
- Google Play store registration costs $25 one-time.
- Apple App Store requires $99/year and is outside the $10–$30 budget.
- For a budget-friendly launch, use Android only or distribute the APK directly.

### 3.8 Use a public backend URL

Mobile builds must point to a reachable API host. Do not use `localhost` inside the mobile app.

- Use your backend domain or IP
- Configure the customer app environment or runtime service URL accordingly

## 4. Deploy the admin app as web or executable

### 4.1 Option A: Web hosting (best low-cost choice)

This is the simplest and cheapest deployment method.

1. Build the admin app:

```bash
cd frontend
ng build yurt-admin --configuration production
```

2. Host the generated files from `dist/yurt-admin/browser`.

3. Recommended free hosts:
   - Netlify
   - Vercel
   - Azure Static Web Apps
   - GitHub Pages (with a simple static server)

4. Ensure the admin app uses the production backend URL.

### 4.2 Option B: Windows executable using Electron

This allows offline-style desktop usage.

1. Install Electron and build tools:

```bash
cd frontend
npm install --save-dev electron electron-builder
```

2. Add a minimal Electron main process script such as `electron-main.js`.

3. Build the Angular admin app for production:

```bash
ng build yurt-admin --configuration production
```

4. Configure `electron-builder` and package the app:

```bash
npx electron-builder --windows
```

5. Distribute the resulting `.exe` or installer file directly.

This method is free to build locally; only distribution costs depend on your delivery channel.

### 4.3 Option C: Windows web wrapper or local desktop deployment

If you want a desktop-like experience without a true `.exe`, host `yurt-admin` as a web app and use a browser shortcut or Progressive Web App wrapper.

## 5. Recommended minimum deployment stack under $30

- Admin web app: free static hosting on Netlify/Vercel
- Customer mobile: Android APK distribution or Google Play for $25
- Backend: small cloud server or platform service under $15/month

## 6. Additional deployment notes

- Ensure CORS accepts the final deployed frontend domains.
- Use HTTPS in production.
- Use a secure JWT secret and production database.
- Keep the frontend API base URL and backend origin in sync.
- If using a shared host, verify the host supports ASP.NET Core 8.

## 7. Example production flow

1. Deploy backend to a small VPS or Azure App Service.
2. Build and publish the admin web app to a free static host.
3. Build the customer Capacitor Android app and distribute the APK or upload to Google Play.
4. Verify that both apps use the deployed backend URL.
