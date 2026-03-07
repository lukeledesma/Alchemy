# Manual Regression Checklist

Use this checklist after refactors where behavior should remain unchanged.

## Home / Navigation
- [ ] Home page loads and recent docs list renders.
- [ ] Open document from home works.
- [ ] Home button from edit page enforces required title, then navigates.

## Editing Basics
- [ ] Edit text cell + `Enter` commits and saves.
- [ ] Edit text cell + `Tab` commits and moves to next text field (skips dropdowns).
- [ ] `Escape` on text cell cancels and restores prior value.
- [ ] No-op commit (same value) does not show false "updated" save status.

## Dropdown Behavior
- [ ] Table dropdown change saves and unhighlights after selection.
- [ ] Selecting same dropdown value still appears unhighlighted.
- [ ] Header protocol dropdown (TCP/RTU) behaves the same.

## Data Type Popup
- [ ] Popup opens from Data Type trigger and aligns to cell border.
- [ ] `Enter` commits and closes popup.
- [ ] `Escape` closes popup without commit.
- [ ] Clicking/focusing outside popup closes it.
- [ ] Popup remains anchored when scrolling/zooming.
- [ ] Tooltip shows exactly two lines:
  - [ ] `Data Type: ...`
  - [ ] `Encode: ...`
- [ ] Known code pairs (for example `105` / `255`) do not show `Unknown` in tooltip labels.

## Table Integrity
- [ ] Duplicate `Address Start` in same register kind highlights red.
- [ ] Invalid numeric fields highlight red.
- [ ] Sort indicator clears after any table data mutation.

## Row Operations
- [ ] Add row works and persists.
- [ ] Select mode: entire row is a single selectable surface.
- [ ] Copy/Cut/Paste/Duplicate selected rows work.
- [ ] Drag/drop reorder works and persists.

## Save Pipeline / Logs
- [ ] Single field edit sends one delta PATCH request.
- [ ] No duplicate full-form PATCH fires after delta save.
- [ ] Server logs include concise `[AlchemyChange]` before/after entries.

## Import / Export Rules
- [ ] Import maps known datatype+encode pairs correctly.
- [ ] Unknown datatype+encode pairs map to `Unique`.
- [ ] Export still produces valid XML and downloads successfully.
