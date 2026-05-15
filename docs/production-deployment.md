# Production Deployment Guide

This document describes production deployment for the Yurt project:
- Backend API
- Admin frontend
- Customer frontend mobile app
- Budget-friendly hosting options around $10–$30

## 1. Deployment goals

- Backend: reliable .NET 8 hosting with secure database access
- Admin: low-cost static web deployment
- Customer: mobile deployment with Android as the primary target
- Total budget target: **$10–$30 initial cost**

## 2. Backend deployment

### 2.1 Build and publish

From the repository root:

```bash
cd backend
dotnet publish src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish
```

### 2.2 Preferred low-cost hosting

#### Option A: Free or low-cost platform
- **Render.com**: free tier for small .NET apps
- **Railway.app**: free tier with small monthly usage
- **Fly.io**: free tier for containerized apps
- **Oracle Cloud Free Tier**: free VM and database options

These are the best choices to stay inside the $10–$30 initial budget.

#### Option B: Cheap VM / VPS
- **Hetzner Cloud**: $5/month
- **Vultr**: $5/month
- **Linode**: $5/month

This is ideal for full control, but adds monthly cost.

### 2.3 Production configuration

Edit `backend/src/Yurt.WebApi/appsettings.json` or use environment variables:

- `ConnectionStrings:DefaultConnection` → production DB connection string
- `Jwt:Secret` → strong secret string
- `AllowedOrigins` → deployed frontend URLs
- `AllowedHosts` → production domain

### 2.4 Run as a service

On Linux, use `systemd`:

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

Then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable yurt-api
sudo systemctl start yurt-api
```

### 2.5 Secure with HTTPS

- Use **Let’s Encrypt** for free TLS certificates
- Configure Nginx or the platform reverse proxy to terminate HTTPS

### 2.6 Budget estimate

- If using free tier hosting: **$0 initial cost**
- If using a cheap VPS: **$5–$15/month**
- If using paid app service: **$10–$30/month** depending on provider

## 3. Admin frontend deployment

### 3.1 Build production assets

```bash
cd frontend
ng build yurt-admin --configuration production
```

### 3.2 Recommended hosting

Use free static hosting:
- **Netlify**
- **Vercel**
- **Azure Static Web Apps**
- **Cloudflare Pages**
- **GitHub Pages**

These services can host the admin app for **free**.

### 3.3 Required production setup

- Ensure the admin app points to the production backend URL
- Ensure backend CORS allows the admin app origin
- Serve the site over HTTPS

### 3.4 Budget estimate

- Hosting: **$0** on free static hosts
- Domain: optional, can use free host-provided URL or purchase separately

## 4. Customer app mobile deployment

### 4.1 Use Capacitor for Android

Install Capacitor and add Android support:

```bash
cd frontend
npm install @capacitor/core @capacitor/cli @capacitor/android
npx cap init "Yurt" "com.yurt.app" --web-dir=dist/yurt-customer/browser
ng build yurt-customer --configuration production
npx cap add android
npx cap sync android
npx cap open android
```

### 4.2 Build and publish Android

- Use Android Studio to build a release APK or AAB
- Sign the release artifact with your key
- Distribute via direct APK sideload or Google Play

### 4.3 iOS considerations

- Requires macOS and Xcode
- Apple Developer Program costs **$99/year**
- Not recommended inside the $10–$30 budget unless you already have access

### 4.4 Backend connectivity

- The mobile app must use your public backend URL
- Do not use `localhost` in production
- Configure the app to point to the deployed backend

### 4.5 Budget estimate

- Direct APK distribution: **$0**
- Google Play registration: **$25 one-time**
- Apple App Store: **$99/year** (not budget-friendly)

## 5. Combined budget plan

### Best $10–$30 initial deployment path

Option A: Minimal cost
- Backend: free hosting or free tier service → **$0**
- Admin frontend: free static hosting → **$0**
- Customer mobile: direct Android distribution → **$0**
- Total: **$0**

Option B: Store-ready mobile launch
- Backend: free hosting or low-cost service → **$0–$5**
- Admin frontend: free static hosting → **$0**
- Google Play account → **$25**
- Total: **$25–$30**

Option C: Cheap VPS + Android store
- Backend: cheap VPS first month → **$5**
- Admin frontend: free static hosting → **$0**
- Google Play account → **$25**
- Total: **$30**

## 6. Deployment checklist

- [ ] Publish backend in Release mode
- [ ] Deploy backend to a reachable host
- [ ] Set production DB connection string
- [ ] Set production JWT secret
- [ ] Set `AllowedOrigins` for frontend domains
- [ ] Build admin frontend for production
- [ ] Deploy admin frontend to static hosting
- [ ] Build customer app with Capacitor for Android
- [ ] Configure mobile app to use production backend
- [ ] Use HTTPS for all production endpoints

## 7. Notes and recommendations

- Prefer static admin hosting and free backend tiers to stay under budget
- Use Android direct APK distribution if you cannot pay for Google Play
- Avoid Apple App Store unless you have a larger budget
- Monitor usage if you choose a free tier host to avoid unexpected charges
