# Alchemy V1 Reference

> Historical reference: this file describes the earlier V1 implementation and
> is retained for design history. For current behavior and backend ownership,
> use [ALCHEMY_BACKEND_GUIDE.md](ALCHEMY_BACKEND_GUIDE.md). For current operator
> instructions, use [ALCHEMY_USER_GUIDE.md](ALCHEMY_USER_GUIDE.md).

This document is a detailed handoff for the Alchemy tool and related Nexus storage-panel behaviors.

## 2026-08-04 Addendum

New companion documentation added during the 2026-08-04 handoff pass:
- `docs/ALCHEMY_HANDOFF_2026-08-04.md` (full top-to-bottom engineering handoff)
- `docs/ALCHEMY_USER_GUIDE.md` (user-facing operational guide)
- `docs/alchemy-reference-assets/README.md` (imported external reference assets and usage)

## Current Product State

Alchemy is running as an independent tool window inside Alchemy with:
- A compact left panel storage browser for file navigation.
- In-window XML loading for Alchemy-compatible files.
- A sortable data table with conflict highlighting, row selection, and copy support.
- Preload-aware grouping and sorting behavior.
- Finder-like context actions (rename/delete/new folder) and drag-move support in the panel.

Nexus main window remains the hub-style app shell while Alchemy acts as a focused, independent tool surface.

## Files Touched for This Iteration

Primary implementation lives in:
- src/Alchemy/AlchemyWindow.axaml
- src/Alchemy/AlchemyWindow.axaml.cs
- src/Alchemy/AlchemyTitleShell.axaml
- src/Alchemy/AlchemyTitleShell.axaml.cs

Cross-reference source used for parity patterns:
- src/Alchemy/MainWindow.axaml
- src/Alchemy/MainWindow.axaml.cs
- src/Alchemy/AlchemyDataCatalog.cs

## Panel Browser Behavior

### Navigation and Visibility
- Panel can be toggled from Alchemy title shell.
- Back/forward folder history is tracked and exposed in title controls.
- Hidden filesystem items are filtered (dotfiles and hidden attributes).

### Context Menus
- Row context menu: Rename, Delete.
- Background/empty-space context menu: New Folder only.
- New Folder appears only when right-clicking empty/in-between space, not on folder rows.

### New Folder + Rename Flow
- Creating a folder immediately opens inline rename on the new folder row.
- Rename validation enforces valid filesystem name constraints.
- Rename error state shows inline invalid styling and tooltip.

### Open Item Highlighting
- Active open file row is highlighted.
- Parent folders of the active file are highlighted up to root.
- Clicking the active file again clears selection and clears table content.

### Drag and Drop in Alchemy Panel
- Files can be dragged onto folder rows to move them.
- Drag visual states:
  - dragged row gets reduced opacity
  - hovered valid folder target gets drop-target highlight
- Name collisions are prevented with user-facing alert.
- If moved file is currently open in Alchemy, it is closed (selection and table are cleared).
- Host OS drag and drop is supported for files:
  - dropping on a folder row targets that folder
  - dropping on valid empty panel area targets current panel level
  - while dragging from host OS, hovering Back/Forward for about 1 second
    navigates panel history so drop destination can be adjusted in-flight
  - external files copy into storage
  - in-storage files use move semantics when valid

### Rename/Delete and Open File State
- Renaming the currently open file updates the title shell text immediately.
- Deleting the currently open file clears active selection.

## Alchemy Table Behavior

### Selection UX
- Table rows are selectable with single, range (Shift), and toggle (Cmd/Ctrl) semantics.
- Active row keyboard navigation with Up/Down works.
- Cmd/Ctrl+A selects all visible rows.
- Cmd/Ctrl+C copies selected rows in tab-delimited format.

### Cursor and Discoverability
- Sortable header buttons use pointer cursor.
- Selectable table rows use pointer cursor.

### Scaling Highlight Rules
- Scaling values `1`, `10`, `100`, and `1000` are treated as valid defaults and are not highlighted.
- Any other scaling value is highlighted purple to draw operator attention.

### Empty State
- If no XML is selected, the table remains empty with no "selected XML" warning.
- If an XML is selected but no supported rows are found, the warning is shown.

## Sorting and Grouping Rules

### Header Interaction
- Sort cycle is three-state:
  1) ascending
  2) descending
  3) default/unsorted
- No chevron in default state.
- Only active sorted column shows chevron.
- Non-active header labels are dimmed while sort is active.

### Chevron Visuals
- Chevron icons are path-based (keyboard arrow up/down style).
- Spacing and vertical alignment are tuned for title-case header labels.

### Preload Blocking
- Preload rows are always grouped into a dedicated top block.
- Non-preload rows are rendered below.
- A divider line is inserted between preload and non-preload blocks.

### Preload Labeling and Sort Metadata
- Preload visible datatype can remain Dummy.
- Hidden sort metadata for preload rows uses coil/holding classification.
- This allows stable ordering behavior without changing displayed text.

### Datatype Ordering Precedence
Datatype sorting is based on Datatype+Encode precedence:
1. Datatype:107 + Encode:255 = BOOL
2. Datatype:107 + Encode:255 = BOOL (Bit of INT)
3. Datatype:0 + Encode:255 = INT
4. Datatype:1 + Encode:255 = UINT
5. Datatype:0 + Encode:102 = INT (Scaled)
6. Datatype:1 + Encode:102 = UINT (Scaled)
7. Datatype:4 + Encode:32 = DINT (Scaled)
8. Datatype:7 + Encode:32 = DINT (Scaled, w/Byte Swap)
9. Datatype:8 + Encode:32 = UDINT (Scaled)
10. Datatype:17 + Encode:32 = UDINT (Scaled, w/Byte Swap)
11. Datatype:4 + Encode:255 = DINT
12. Datatype:7 + Encode:4 = DINT (w/Byte Swap)
13. Datatype:8 + Encode:255 = UDINT
14. Datatype:17 + Encode:8 = UDINT (w/Byte Swap)
15. Datatype:0032 + Encode:255 = REAL
16. Datatype:0035 + Encode:32 = REAL (w/Byte Swap)

### Address Sorting
Address sorting now:
- Splits into register scope blocks:
  - Coil status block on top
  - Holding register block below
- Inside each scope block:
  - address is numeric-first (not lexical)
  - datatype precedence is used as tie-breaker
- This prevents lexical mistakes such as 1,11,2 ordering.

## Important Implementation Notes

- `AlchemyWindow.axaml.cs` now carries the majority of interaction and sorting logic.
- `AlchemyDataCatalog.Normalize` trims leading zeros so code comparisons handle values like 0032 correctly.
- File move/rename/delete operations are guarded for IO and permission failures with user-facing alerts.
- `scripts/run.sh` behavior can vary under VS Code sandbox wrappers; local terminal execution remains canonical for runtime validation.
- Folder dimming in the Alchemy panel follows a three-state standard:
  - `HasFiles`: full brightness
  - `FolderOnly` (contains only folders, no files in subtree): 60% content opacity
  - `Empty` (no files and no folders): 40% content opacity
- While a row is being renamed, dimming is disabled so rename affordance always appears at full contrast.
- External panel drag handlers are registered at window scope so host drag
  updates continue when the pointer moves from panel content to title controls.
- Back/forward drag-hover hit-testing uses title-shell coordinate space derived
  from the window pointer position.

## Cross-Surface Standardization Notes

Alchemy panel storage behavior is intentionally aligned with Nexus storage workspace for:
- folder dimming state model
- rename-mode visibility override
- root versus folder drop targeting semantics
- dashed target affordances for row-level versus area-level drops

## Suggested Verification Checklist

1. Open Alchemy and toggle panel.
2. Navigate folder tree with back/forward.
3. Drag a host OS file and hover Back for about 1 second; confirm history steps back.
4. Continue dragging and hover Forward for about 1 second; confirm history steps forward.
5. Right-click row (rename/delete only) and empty space (new folder only).
6. Create folder and confirm immediate inline rename.
7. Rename open file and verify title updates.
8. Drag file into folder and verify move + close-if-open behavior.
9. Open XML and validate parent/active highlighting in panel.
10. Click active file again and verify table clears.
11. Sort each column through asc/desc/default.
12. Validate datatype precedence exactly matches configured block order.
13. Validate address sorting: coil block above holding block, numeric ordering inside each.

## Next Safe Enhancements

- Auto-expand folder on drag hover delay for deeper moves.
- Optional visual badge to indicate preload rows versus regular rows.
- Optional diagnostics overlay for Datatype+Encode pair display during sort QA.
