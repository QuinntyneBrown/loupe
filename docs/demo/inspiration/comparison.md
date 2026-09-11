# Inspiration UI compared with the mock

Reference: `docs/mocks/inspiration.html?chrome=0`. Implementation: the current Angular `/inspiration` route with Playwright service fixtures. Both captures use the same first twelve cached Picsum images at a 1440 × 900 viewport.

| Area | Mock | Current UI |
| --- | --- | --- |
| Collection | Four columns beside a sidebar | Five columns across the main content area at this viewport |
| Organisation | Boards sidebar, board counts and tag chips | No boards sidebar or tag filters |
| Card captions | Title and photographer label on hover | Title, saved date and reference status |
| Card actions | Add to boards and open source overlays | Open the reference detail through the card title |
| Saving | One Save reference entry point | Separate Save link and Upload reference entry points |
| Navigation | My Work, Inspiration, Photographers and Search | My Work and Inspiration |
| Detail workflow | Not represented by this collection mock | Source, attribution, editable notes and metadata; separate source import action |
| Small screens | Mock is a design reference | Recorded real collection at 375 pixels; no horizontal overflow |

The video shows the mock, the working page, and a labelled side-by-side comparison. It also exercises pagination, metadata saving, link-only saving, and retry after a simulated loading failure. Board management and tag filtering are visibly absent from the current collection; this recording does not claim they are implemented. Source import is displayed but never requested. Upload is an available entry point, not a demonstrated completed upload.

All reference records are in-memory fixtures. The supplied titles and photographer names are fictional mock labels, not verified image credits. Public placeholder photographs are cached under `e2e/demo/inspiration/images`; their original URLs are recorded in `images.json`. No application code was altered for the video, and no production records or AI services were used.
