# Loupe design system

Requires Node 24.18.0. This static site works with the application and API stopped.

```powershell
cd design-system
npm ci
npx playwright install chromium
npm test
npm run build
npm run preview
```

Publish the contents of `dist/` to any static host. No credentials or runtime API
are needed. `src/tokens.css` owns Loupe's visual tokens, seeded from the mockups.
The reference includes buttons, forms, modal editing, selected tags, status pills,
synthetic image grids, progress, and empty/error examples. Preview uses port 4187.
The build reads `src/breakpoints.json` to generate the specified grid rules.
Acceptance progress and remaining manual release checks are in `../tasks/todo.md`.

## Keyword search

Open **Keyword search** from the reference navigation, or visit `/search.html`.
Its local synthetic library demonstrates initial, mixed-result, loading, empty,
failure, unavailable-filter, and pagination-recovery states. The state selector
sets a starting condition; search, type selection, board/tag drafts, Apply,
Cancel/Escape, draft Clear, active-filter removal, Load more, and Retry really
change the example. Held loading completes with **Complete loading**.
Tags require every selected tag; boards match any selected board. Each draft
group permits at most ten selections, and the dialog contains keyboard focus.
Explicit paging/retry recovery moves focus only if the user has not moved it.

Keyword matching uses titles, tags, and notes, not image meaning. There is no
semantic provider or runtime application dependency. Geometric previews are
local CSS, saved-item actions open synthetic details, and separate external
source actions use reserved `example.com` URLs with safe new-window attributes.
The search grid follows the HTML mock: two columns below 640px, auto-fill
220px-minimum tiles above it, and photographer cards spanning two columns.
This does not change the older foundation gallery's breakpoint examples.

Run the independent Chromium search acceptance on a free isolated port:

```powershell
$env:LOUPE_DESIGN_PORT = '4199'
npm test -- search.spec.js search-filters.spec.js search-recovery.spec.js search-layout.spec.js --project=chromium --output=artifacts\search-test-results
```

The runner builds and owns its preview server unless
`LOUPE_DESIGN_EXTERNAL_SERVER=1` explicitly selects an already-owned server.
Layout acceptance includes 320px reflow, both sides of 640/1024px, narrow and
short-height dialogs, keyboard focus, text spacing, reduced motion, and Axe.
Automated checks do not establish full screen-reader conformance.

## Locations

Open **Locations** from the reference navigation, or visit `/locations.html`.
The page shows location cards with every report status and the no-image
placeholder, Find a location result cards on the one/two/three/three/four column
rule with the Keyword/Meaning control, shoot-type chips and the Matched by meaning
pill, a gallery whose radio thumbnails switch the full-frame stage by click or
arrow key with the selection announced and a Set as cover action, a scouting
report with its six sections, rating and evidence-basis pills and cited-image
buttons that select the gallery image, and the search-index status pills with a
Retry action. Everything is synthetic and local; no request leaves the page.
