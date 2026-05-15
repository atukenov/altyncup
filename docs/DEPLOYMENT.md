# Yurt App – Deployment & Conversion Guide

This document explains three distribution strategies for the Yurt project:

1. **Customer app → Native mobile app** (Android & iOS) using Capacitor
2. **Admin app → Desktop executable** (Windows / macOS) using Electron
3. **Admin app → Browser / static hosting** (simplest option)

---

## 1. Customer App → Mobile App (Capacitor)

[Capacitor](https://capacitorjs.com/) wraps any web app as a native Android or iOS app with access to device APIs (camera, push notifications, etc.). It is the official Ionic approach and works perfectly with Angular.

### Prerequisites

| Tool | Version |
|------|---------|
| Node.js | 18+ |
| Angular CLI | Already installed |
| Android Studio | Latest stable (for Android) |
| Xcode | Latest (macOS only, for iOS) |
| Java JDK | 17+ (for Android Gradle) |

---

### Step-by-step: Android

**1. Install Capacitor in the customer project**

```bash
cd frontend
npm install @capacitor/core @capacitor/cli @capacitor/android
```

**2. Initialise Capacitor**

Run this once from the `frontend/` folder:

```bash
npx cap init "Yurt" "com.yurt.app" --web-dir=dist/yurt-customer/browser
```

- `"Yurt"` – App display name
- `"com.yurt.app"` – Bundle ID (reverse-domain, unique per app store)
- `--web-dir` – The folder Angular outputs to after `ng build`

**3. Build the Angular app for production**

```bash
ng build yurt-customer --configuration production
```

Output will be at `dist/yurt-customer/browser/`.

**4. Add the Android platform**

```bash
npx cap add android
```

This creates an `android/` folder at the root of `frontend/`.

**5. Sync web assets into the native project**

```bash
npx cap sync android
```

Run this every time you rebuild the Angular app.

**6. Open in Android Studio**

```bash
npx cap open android
```

In Android Studio:
- Connect a physical device or start an emulator.
- Press ▶ Run to build and install the APK.
- Use **Build → Generate Signed APK / App Bundle** to create a release build.

---

### Step-by-step: iOS (macOS only)

```bash
npm install @capacitor/ios
npx cap add ios
npx cap sync ios
npx cap open ios
```

In Xcode, select your target device and press ▶. For App Store distribution use **Product → Archive**.

---

### Connecting to the backend

In a real device build the app cannot reach `http://localhost:5000`. You must either:

- **Development**: Use your machine's local IP, e.g. `http://192.168.1.x:5000` and allow it in the backend CORS policy.
- **Production**: Deploy the .NET backend to a server (Azure App Service, VPS, etc.) and update `environment.production.ts` with the real URL.

> Update `frontend/projects/yurt-customer/src/environments/environment.ts` (and `.prod.ts`) with the correct `apiUrl` and `signalrUrl` before building.

---

### Useful Capacitor commands

```bash
npx cap sync          # copy latest web build to all native platforms
npx cap copy android  # copy only (skip plugin updates)
npx cap run android   # build + deploy to connected device in one step
```

---

## 2. Admin App → Desktop Executable (Electron)

[Electron](https://www.electronjs.org/) bundles a Chromium browser and your web app into a standalone `.exe` / `.dmg` / `.AppImage`.

### Prerequisites

- Node.js 18+
- Angular CLI

---

### Step-by-step

**1. Build the admin Angular app**

```bash
cd frontend
ng build yurt-admin --configuration production
```

Output will be at `dist/yurt-admin/browser/`.

**2. Create a minimal Electron project**

You can place this alongside or outside the Angular workspace. Example using a new `electron-admin/` folder at the repo root:

```bash
mkdir ..\electron-admin
cd ..\electron-admin
npm init -y
npm install --save-dev electron electron-builder
```

**3. Create `main.js` (Electron entry point)**

```js
// electron-admin/main.js
const { app, BrowserWindow, protocol } = require('electron');
const path = require('path');

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
    },
    title: 'Yurt Admin',
    icon: path.join(__dirname, 'icon.png'), // optional
  });

  // Load the built Angular app
  win.loadFile(path.join(__dirname, '../frontend/dist/yurt-admin/browser/index.html'));
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow();
});
```

> **Important**: Angular apps use client-side routing. If the app shows a blank screen, you may need to enable `HashLocationStrategy` in the admin Angular app, or configure Electron to use a custom protocol. The simplest fix is to add `useHash: true` to `RouterModule.forRoot(routes, { useHash: true })` in the admin app's router config.

**4. Add scripts to `package.json`**

```json
{
  "main": "main.js",
  "scripts": {
    "start": "electron .",
    "dist": "electron-builder"
  },
  "build": {
    "appId": "com.yurt.admin",
    "productName": "Yurt Admin",
    "win": {
      "target": "nsis"
    },
    "mac": {
      "target": "dmg"
    },
    "linux": {
      "target": "AppImage"
    },
    "files": [
      "main.js",
      "../frontend/dist/yurt-admin/**"
    ]
  }
}
```

**5. Run in development**

```bash
cd electron-admin
npm start
```

**6. Build the installer**

```bash
npm run dist
```

`electron-builder` outputs an installer to `dist/` inside `electron-admin/`. On Windows this produces a `.exe` setup file.

---

### Connecting to the backend

The same note applies as Capacitor: update the admin Angular `environment.prod.ts` to point to the real backend URL before running `ng build`.

---

## 3. Admin App → Browser / Static Hosting

This is the simplest approach and requires no extra tools. The admin app is already a standard Angular SPA that runs in any modern browser.

### Build

```bash
cd frontend
ng build yurt-admin --configuration production
```

### Serve locally (quick test)

```bash
npx serve dist/yurt-admin/browser
```

### Deploy options

| Option | How |
|--------|-----|
| **nginx** | Copy `dist/yurt-admin/browser/` to the nginx `html` folder. Add a `try_files $uri $uri/ /index.html` rewrite for Angular routing. |
| **Apache** | Copy to `htdocs/`. Add `.htaccess` with `FallbackResource /index.html`. |
| **IIS** | Copy to `wwwroot/`. Add a URL rewrite rule to redirect all 404s to `index.html`. |
| **GitHub Pages** | Push the `dist/yurt-admin/browser/` folder to a `gh-pages` branch (use `angular-cli-ghpages`). |
| **Azure Static Web Apps** | Connect the GitHub repo; configure `output_location: dist/yurt-admin/browser`. |
| **Netlify / Vercel** | Drag-and-drop the `dist/yurt-admin/browser/` folder or connect your repo with build command `ng build yurt-admin --configuration production`. Add a `_redirects` / `vercel.json` rewrite for Angular routing. |

### nginx example config snippet

```nginx
server {
    listen 80;
    server_name admin.yurt.example.com;
    root /var/www/yurt-admin;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

---

## Summary

| Goal | Tool | Complexity |
|------|------|-----------|
| Customer app → Android APK | Capacitor + Android Studio | Medium |
| Customer app → iOS app | Capacitor + Xcode | Medium (macOS required) |
| Admin app → Windows .exe | Electron + electron-builder | Medium |
| Admin app → Browser hosting | nginx / IIS / cloud | Low |
