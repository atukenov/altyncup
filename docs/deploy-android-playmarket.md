# Deploy Android App to Google Play Market

**App:** Altyncup (`com.yurt.app`)  
**Min SDK:** 23 (Android 6.0) · **Target SDK:** 35 (Android 15)

---

## Prerequisites

- [ ] Google Play Developer account ($25 one-time fee) — [play.google.com/console](https://play.google.com/console)
- [ ] Java JDK 17+ installed (`java -version`)
- [ ] Android Studio installed with SDK tools
- [ ] App listing created in Play Console (name, description, screenshots, icon)

---

## Step 1 — Build the Web App

```bash
cd frontend
npx ng build yurt-customer --configuration production
```

Output goes to `dist/yurt-customer/browser/`.

---

## Step 2 — Sync to Android

```bash
npx cap sync android
```

This copies the built web assets into the Android project and updates any native plugins.

---

## Step 3 — Create a Signing Keystore (first time only)

> **Keep the keystore file and passwords safe — you can never change it once the app is published. Losing it means you cannot update the app.**

```bash
keytool -genkey -v \
  -keystore altyncup-release.keystore \
  -alias altyncup \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000
```

You will be prompted for:
- Keystore password (store this securely)
- Key alias password
- Name, organisation, country details

Store `altyncup-release.keystore` somewhere **outside** the git repository.

---

## Step 4 — Configure Signing in the Android Project

Open `frontend/android/app/build.gradle` and add inside the `android {}` block:

```groovy
android {
    ...
    signingConfigs {
        release {
            storeFile file("/path/to/altyncup-release.keystore")
            storePassword "YOUR_STORE_PASSWORD"
            keyAlias "yurt"
            keyPassword "YOUR_KEY_PASSWORD"
        }
    }
    buildTypes {
        release {
            signingConfig signingConfigs.release
            minifyEnabled false
            proguardFiles getDefaultProguardFile('proguard-android.txt'), 'proguard-rules.pro'
        }
    }
}
```

> Alternatively, use environment variables or `local.properties` to avoid committing passwords to git.

---

## Step 5 — Bump the Version

In `frontend/android/app/build.gradle`:

```groovy
defaultConfig {
    versionCode 2        // increment by 1 for every release
    versionName "1.1.0"  // semantic version shown to users
}
```

`versionCode` must always be higher than the previous release.

---

## Step 6 — Build the Release AAB

Google Play requires **Android App Bundle (.aab)**, not APK.

### Option A — Android Studio
1. Open `frontend/android/` in Android Studio
2. **Build → Generate Signed Bundle / APK**
3. Choose **Android App Bundle**
4. Select your keystore and fill in the passwords
5. Choose **release** build variant
6. Click **Finish** — the `.aab` is saved to `android/app/release/app-release.aab`

### Option B — Command Line

```bash
cd frontend/android
./gradlew bundleRelease
```

Signed AAB will be at:
```
android/app/build/outputs/bundle/release/app-release.aab
```

---

## Step 7 — Upload to Google Play Console

1. Go to [play.google.com/console](https://play.google.com/console) → select **Yurt**
2. Navigate to **Release → Testing → Internal testing** (first release) or **Production**
3. Click **Create new release**
4. Upload `app-release.aab`
5. Fill in **Release notes** (what changed in this version)
6. Click **Save → Review release → Start rollout**

### Release tracks

| Track | Use for |
|---|---|
| **Internal testing** | Team testing (up to 100 testers), instant publish |
| **Closed testing (Alpha)** | Wider group of selected testers |
| **Open testing (Beta)** | Public opt-in testers |
| **Production** | Full public release |

Start with **Internal testing** to verify everything works before going to production.

---

## Step 8 — App Content Requirements (one-time setup)

Before the first production release, complete in Play Console:

- **App content** → Privacy policy URL (required)
- **App content** → Data safety form (what data the app collects)
- **Store listing** → Add screenshots for phone (min 2), and a 512×512 icon
- **Store listing** → Short description and full description in Russian/Kazakh/English

---

## Updating the App Icon for Android

Replace these files with your icon (use `altyncup-logo.jpg` resized):

| File | Size |
|---|---|
| `android/app/src/main/res/mipmap-mdpi/ic_launcher.png` | 48×48 |
| `android/app/src/main/res/mipmap-hdpi/ic_launcher.png` | 72×72 |
| `android/app/src/main/res/mipmap-xhdpi/ic_launcher.png` | 96×96 |
| `android/app/src/main/res/mipmap-xxhdpi/ic_launcher.png` | 144×144 |
| `android/app/src/main/res/mipmap-xxxhdpi/ic_launcher.png` | 192×192 |

Quick resize with `sips` (macOS):

```bash
SOURCE="frontend/projects/yurt-customer/public/altyncup-logo.jpg"
ANDROID="frontend/android/app/src/main/res"

for size in 48 72 96 144 192; do
  case $size in
    48)  dir="mipmap-mdpi" ;;
    72)  dir="mipmap-hdpi" ;;
    96)  dir="mipmap-xhdpi" ;;
    144) dir="mipmap-xxhdpi" ;;
    192) dir="mipmap-xxxhdpi" ;;
  esac
  sips --resampleHeightWidth $size $size "$SOURCE" \
    --out "$ANDROID/$dir/ic_launcher.png"
  sips --resampleHeightWidth $size $size "$SOURCE" \
    --out "$ANDROID/$dir/ic_launcher_round.png"
done
```

---

## Common Errors

| Error | Fix |
|---|---|
| `versionCode` already used | Increment `versionCode` in `build.gradle` |
| APK uploaded instead of AAB | Use `bundleRelease` not `assembleRelease` |
| Signing key mismatch | You must always use the same keystore |
| `minSdkVersion` too high | Current setting is 23 (Android 6), which covers 99%+ devices |
| Policy violation | Review [Play policies](https://play.google.com/about/developer-content-policy/) before submitting |

---

## Release Checklist

```
[ ] ng build --configuration production (no errors)
[ ] npx cap sync android
[ ] versionCode incremented
[ ] versionName updated
[ ] Signed AAB generated
[ ] Tested on a real device or emulator
[ ] Release notes written
[ ] Uploaded to Play Console
[ ] Internal testing passed
[ ] Rolled out to production
```
