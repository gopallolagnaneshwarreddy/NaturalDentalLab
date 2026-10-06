# Natural Dental Labs — visual redesign patch

## What this actually is
Your zip is **DentalLab.Web**, a Blazor Server case-management and billing
console — not a public marketing website. That matters because the brief
(hero banners, "abstract branding" image prompts, glassmorphism on a
landing page) was written for a brochure site. I've adapted the same
intent — premium, clinical-but-warm, teal/gold, modern motion and glass —
to the app you actually have: sidebar, dashboard, login screen, cards,
tables, buttons.

## What I found before touching anything
- `wwwroot/app.css` is the **only stylesheet actually linked** (see
  `Components/App.razor`). It already implements a genuinely polished
  design system: a teal/gold "Natural Dental" palette, Plus Jakarta
  Sans + Inter type, a gradient sidebar, a custom SVG tooth mark, and a
  glass-accented split-panel login screen. This is a strong base — most
  of the brief's requirements (clean palette, premium type, whitespace,
  grid layouts, iconography) were already met.
- `wwwroot/app-professional.css` and `wwwroot/dashboard-professional.css`
  were **never referenced anywhere** (not in `App.razor`, not in any
  `.razor` file) — a leftover blue "tech" theme that conflicts with the
  live teal/gold one. I removed both so nobody accidentally re-links a
  clashing theme later.
- The zip contains no photography, icons, or branding artwork beyond
  `favicon.png`. The Midjourney/DALL-E prompts you included are good —
  I've kept them below for when you're ready to generate imagery; there's
  currently nowhere in the app that would use large photographic hero
  images (it's an internal tool, not a landing page), so I didn't force
  them in.

## Bug fix (the sidebar washout)
`Components/Layout/MainLayout.razor.css` defined its colors as CSS custom
properties inside a `:root { ... }` block. That file is a **Blazor
scoped stylesheet**, and Blazor's CSS isolation rewrites every selector
to include a scope attribute — so that block compiled to
`:root[b-xxxxxxxx] { ... }`. That selector matches nothing, because the
scope attribute is only ever applied to elements this component
renders, and `<html>` (the real root) is rendered by `App.razor`, a
different component. The result: `--sidebar-bg` and friends were never
actually defined anywhere, so `.sidebar { background: var(--sidebar-bg); }`
silently resolved to nothing, and the sidebar fell through to the page's
pale background — which is exactly the washed-out look in your
screenshot. This wasn't a color choice, it was dead CSS.

Fix: I rewrote `MainLayout.razor.css` with every value hardcoded
directly on its selector (no dependency on a `:root` block), using the
exact same deep-teal gradient as your login page's brand panel
(`#0b5852 → #0f766e → #115e59`). The sidebar options should now render
clearly against a solid teal background, matching the login screen.

## New: Doctor Bill page (`/doctor-bill`)
A new page, restricted to Owner/Admin (it shows amounts, like the rest of
billing), that generates a printable per-doctor bill:

- Pick a doctor and a date range (defaults to the current month).
- It pulls that doctor's **active** cases for the range using the same
  `ICaseService.SearchAsync` your Cases page already uses, and paginates
  through automatically if there are more than one page's worth.
- The table is a trimmed version of the Cases table — **Case ID, Date,
  Patient, Teeth/Arch, Type, Unit Rate, Units, Unit Amount, Total
  Amount** — grouped by case exactly like Cases does (no Doctor column,
  since the whole bill is for one doctor; no Created By/Status/View,
  since those aren't relevant on a bill).
- Below the table: **current period total**, an editable **Old due**
  field, and a **Grand total** (period total + old due) highlighted in
  teal.
- A **Print bill** button calls the browser's print dialog. Printing
  hides the sidebar, top bar, and all the filter/button chrome (new
  `@media print` rules in `app.css`) so only the letterhead, table, and
  totals go to paper/PDF.

New/changed files for this:
- `Components/Pages/DoctorBilling/Index.razor` (new)
- `Components/Pages/DoctorBilling/Index.razor.css` (new)
- `Components/Layout/NavMenu.razor` — added a "Billing → Doctor Bill"
  link, Owner/Admin only
- `wwwroot/app.css` — added the `@media print` block

### Important: "Old due" is entered by hand, not stored
Your requirements notes mention automatic carry-forward of unpaid prior
balances — a persisted running balance per doctor. **I don't have that
in the zip**: only the `DentalLab.Web` project was uploaded, not
`DentalLab.Application`/`DentalLab.Infrastructure`, where a doctor's
balance would actually live and where `ICaseService`, `CaseFilter`,
`CaseListItem`, `Doctor`, and `PagedResult<T>` are defined. So for now,
Old Due is a manual number you type in each time you generate a bill —
it isn't looked up or saved anywhere. If you want it to auto-fill from
last month's unpaid balance, share those two projects (or just the
service interfaces) and I'll wire it up to pull and persist a real
running balance instead of a text box.

### Build errors from the first version of this file — fixed
The first pass guessed two field types wrong (I didn't have the
`Application` project to check against): `CaseFilter.DocId` is actually
`int?` (not `string`), and `CaseFilter.FromDate`/`ToDate` are `DateOnly?`
(not `DateTime?`). Both are corrected in this version — the doctor
dropdown now compares/clears as `int?`, and the date fields build
`DateOnly` values. I also added a missing `@using
DentalLab.Web.Components.Pages.Dashboard` so `<EmptyState>` resolves
(it lives in the Dashboard folder's namespace). If you already dropped
the previous version into your project, just replace
`DoctorBilling/Index.razor` with this one — nothing else changed.

## Visual redesign details (styling only)
1. **`wwwroot/app.css`** — added a `rise-in` entrance keyframe, a
   `.glass-panel`/`.glass-panel-dark` utility (frosted, saturated blur),
   a `prefers-reduced-motion` guard, hover-lift on `.card` and
   `.btn-primary`, and the `@media print` block used by the Doctor Bill
   page below.
2. **`Components/Layout/MainLayout.razor.css`** — the top bar is now a
   sticky frosted-glass strip (`backdrop-filter: blur`) instead of flat
   white; the sidebar gets a subtle radial sheen (plus the `:root` bug
   fix above).
3. **`Components/Layout/NavMenu.razor.css`** — nav links glide right and
   their icon nudges on hover; the active link gets a glass background
   instead of a flat tint.
4. **`Components/Pages/Dashboard/Index.razor.css`** — metric cards and
   panel cards lift and gain depth on hover with a staggered fade-in on
   load; the daily-production bars glow on hover; the hero card's ring
   motif drifts gently; case-type rows and approval-list rows get a
   micro slide on hover.
5. **`Components/Account/Pages/Login.razor.css`** — two soft frosted
   orbs drift slowly behind the brand panel; the brand mark and login
   card fade/rise in on load.

## How to apply everything in this zip
Drop all files into your project at their matching paths (they overwrite
the existing ones 1:1, except `DoctorBilling/Index.razor(.css)` which are
new), add the "Billing" nav entry from the updated `NavMenu.razor`, then
delete `wwwroot/app-professional.css` and `wwwroot/dashboard-professional.css`
from your own copy. Nothing outside these files needs to change.

## Image-generation prompts (kept for later)
- **Hero / marketing use only** (e.g. a future public site, not this
  console): "A modern, high-tech dental laboratory, clean and brightly
  lit with natural sunlight, sleek white counters, advanced dental
  milling machines softly glowing, a calming teal and white color
  palette, professional, pristine, 8k resolution, photorealistic,
  architectural photography --ar 16:9"
- **Abstract branding** (could work as a subtle background texture in
  the login panel or a print letterhead): "Abstract, elegant 3D
  rendering of a flawless dental crown incorporating subtle organic and
  natural leaf-like curves, glowing soft blue and white lighting,
  premium healthcare branding, minimalist, high detail, clean white
  background."

## If you actually want a marketing site too
This app has no public-facing pages at all — everything sits behind
login. If Natural Dental Labs also wants a separate marketing site
(the kind the original brief describes, with a hero banner and product
showcase), that's a distinct, smaller build — say the word and I'll draft
one as its own page rather than retrofitting it into the console.
