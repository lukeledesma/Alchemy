# Latest Change

- 2026-03-11 01:26: Added `.xml.tar.gz` import support by extending archive detection in `DocumentsController#resolve_import_path`, updating import file picker accept lists in `new` and organizer views, and adding integration coverage for tar.gz uploads.
- 2026-03-11 01:40: Fixed preload export length math for non-zero addresses so bit/coil preloads use range delta to last address and word preloads use range delta plus one; also added exporter regression tests covering `800..807` behavior.
- 2026-03-11 02:09: Fixed datatype/encode hover text formatting to avoid duplicated code prefixes and incorrect fallback codes, and added about three rows of bottom spacing under the tag table for easier last-row editing.
- 2026-03-11 02:10: Made tar.gz import integration test filename assertion collision-safe so existing files no longer cause false failures when import auto-renames to `sample_export N.xml`.
- 2026-03-11 02:14: Moved extra bottom spacing out of the table body and into outside container margin so the grid ends at the last row while keeping ~3.5 rows of breathing room below the bordered table.
- 2026-03-11 02:16: Increased bottom outside spacing by one additional row equivalent (from ~3.5 rows to ~4.5 rows) beneath the table container.
- 2026-03-11 02:18: Increased bottom outside spacing by another half-row equivalent (from ~4.5 rows to ~5 rows) beneath the table container.
- 2026-03-11 02:27: Replaced static bottom spacing with dynamic datatype-popup padding that only applies for bottom rows: last row adds ~5 rows, second-last ~4, third-last ~3, fourth-last ~2, and fifth-or-higher adds none.
- 2026-03-11 02:33: Refined datatype-popup spacing to be viewport-gap based (not fixed row steps): for only the last 4 rows, add just enough outside padding so popup bottom keeps a consistent gap from the screen bottom.
- 2026-03-11 02:37: Fixed popup scroll jitter by locking calculated bottom padding while scrolling (reposition-only on scroll) and recalculating padding only on open/viewport resize for the last 4 rows.
- 2026-03-11 02:41: Improved bottom-row datatype workflow by preserving padding when switching between datatype triggers and auto-aligning viewport for last-4-row popups, preventing snap-back and manual re-scroll between adjacent bottom rows.
- 2026-03-11 02:45: Removed automatic viewport snap and changed last-4-row popup spacing to stepped row-based padding (last row highest, then minus one step per row upward) so users can manually scroll once, then climb rows without reset/cutoff.
- 2026-03-11 02:58: Kept stepped last-4-row popup behavior and added baseline spacing equal to default table cell vertical padding so popup extension looks uniform with table padding.
- 2026-03-11 03:02: Replaced stepped last-4-row popup extension with a thinner, uniform padding amount (same thickness for last, second-last, third-last, and fourth-last rows) to keep spacing visually consistent.
- 2026-03-11 03:05: Restored climb behavior for last-4-row popups by using popup-height baseline minus one row-height per row upward (with minimum gap clamp), so each higher row reduces extension instead of staying at max thickness.
- 2026-03-11 03:08: Reduced popup extension thickness by switching last-4-row climb steps to use default page edge padding as the unit (thinner, layout-consistent spacing while preserving climb behavior).
- 2026-03-11 03:12: Reverted from page-edge-padding scaling back to the prior popup-height climb formula and reduced computed popup extension by 50% to keep climb behavior while avoiding lower-row cutoff.
- 2026-03-11 03:16: Corrected popup extension step behavior so row-to-row changes now subtract a full row height (matching popup jump) while only halving popup-height base contribution; fixes small-increment padding mismatch.
- 2026-03-11 03:19: Updated last-4-row popup spacing to use the current 4th-row padding as the baseline, then increment by one full row-height for each step toward the bottom row.
- 2026-03-11 03:23: Set popup baseline bottom gap from 24px to 20px so 4th-row anchor aligns with default workspace/page padding.
- 2026-03-11 03:24: Tuned popup baseline bottom gap from 20px to 19px for finer visual alignment.
- 2026-03-11 03:30: Removed legacy auto-`Imported` fallback import path: imports now require a valid existing destination folder (`parent_id`), organizer hides orphan on-disk `Imported` folder entries, and integration tests were updated/expanded for the new rule.

====================
## Developer Notes
