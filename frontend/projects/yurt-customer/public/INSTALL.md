# ALTYNCUP badge + avatar assets — install guide

52 PNGs, 512×512, transparent background. 28 badges (deduplicated from the 40 slices) and 24 avatars. Checked by hash on install: all 52 files are byte-distinct, so nothing here was actually a duplicate.

**Status (2026-09-12): done.** Steps 1–4 below are implemented — badges wired into `achievements.ts` with real claim logic (see `ACHIEVEMENTS.md` for which of the 28 are live vs. still blocked on a missing feature or item-classification data), and the avatar picker is live in Profile, storing the choice in `localStorage` per the original suggestion in step 4.

## 1. Copy into the app

Your project serves `public/` at the web root (Angular 18+ style), so:

```
cp -r badges  altyncup/frontend/projects/yurt-customer/public/badges
cp -r avatars altyncup/frontend/projects/yurt-customer/public/avatars
```

They are then reachable as `/badges/first-drink.png` and `/avatars/avatar-01.png`. No `angular.json` change needed — `public/` is already in the `assets` config alongside `logo.png`, `kaspi.png` etc.

## 2. Add a `badge` field to the achievement model

`src/app/features/news/achievements.ts` currently uses an emoji `icon`. Add a `badge` path next to it (keep `icon` as the fallback):

```ts
export interface Achievement {
  id: string;
  icon: string;            // keep — fallback if the image fails or none is assigned
  badge?: string;          // NEW — path under /badges, optional
  nameEn: string;
  // …unchanged
}
```

(`badge` ended up optional rather than required — `wolt_rider` has no art of its own once `globetrotter.png` was repointed to a new location-based achievement, so it needs the emoji-only fallback path.)

Then fill it in for the nine existing achievements. These map to the art by meaning:

| id | badge |
| --- | --- |
| `first_sip` | `/badges/first-drink.png` |
| `coffee_rookie` | `/badges/coffee-beginner.png` |
| `golden_cup` | `/badges/gold-cup.png` |
| `altyn_regular` | `/badges/constant.png` |
| `altyncup_star` | `/badges/star-of-altyncup.png` |
| `century_sipper` | `/badges/hundred-cups.png` |
| `big_spender` | `/badges/coffee-baron.png` |
| `altyn_champion` | `/badges/champion.png` |
| `wolt_rider` | `/badges/globetrotter.png` |

`condition` functions stay exactly as they are — this is an art swap, not a logic change.

## 3. Swap the emoji for the image in the template

In `achievements.component.html`, replace the emoji span with:

```html
<img [src]="a.badge" [alt]="name(a)" width="64" height="64"
     class="w-16 h-16 object-contain"
     [class.grayscale]="!isUnlocked(a)"
     [class.opacity-40]="!isUnlocked(a)">
```

`grayscale` + `opacity-40` gives the locked state for free — no second set of files. Add `transition-all duration-300` if you want the unlock to animate when stats refresh.

Keep `loading="lazy"` off for the first screenful; the images are small (~40–80 KB each) but 28 at once is worth lazy-loading below the fold:

```html
<img [src]="a.badge" loading="lazy" …>
```

## 4. Avatars (profile picker)

The 24 avatars are interchangeable choices, not achievements. Store the chosen filename on the customer record (or `localStorage` until there's a backend field):

```ts
readonly AVATARS = Array.from({ length: 24 }, (_, i) =>
  `/avatars/avatar-${String(i + 1).padStart(2, '0')}.png`);
```

Render in a circular crop — the art is already composed for it, with safe margins:

```html
<img [src]="avatar" class="w-20 h-20 rounded-full object-cover">
```

## 5. The 19 unused badges

`badges/` holds 28 files; only 9 are wired to existing achievements. The other 19 (early-riser, weekend-warrior, milk-artist, streak-master, perfect-brew, …) are ready for when the matching achievements are defined. Proposed unlock criteria for every one are in `asset-library.html` in the design project — those are suggestions to confirm, not decisions.

## Caveat on resolution

The source sheets rendered each badge at roughly 120–130 px. These files are upscaled to 512×512, so they are sharp at the 64–128 px sizes the app uses, and soft if ever shown much larger. If you need a full-resolution hero treatment (an unlock celebration screen at 256 px+), regenerate that specific badge at high resolution rather than scaling these up.
