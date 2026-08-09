# Alchemy User Manual

Status: Production user manual

Last updated: 2026-08-08

Alchemy is a desktop tool for creating and repairing UTICOR Modbus router files.
It keeps the tag table always editable, hides imported preload rows, and rebuilds
valid preload blocks on save.

## 1. Quick start (most common workflow)

1. Launch the app with scripts/run.sh.
2. Open an existing file from File > Open, drag one in, or click one in the storage panel.
3. Edit rows directly in the table.
4. Fix any highlighted validation issues.
5. Save with Cmd/Ctrl+S.

For a brand-new file:
1. Start with the empty table.
2. Add a row from right-click > Add Row or press Cmd/Ctrl +.
3. Fill all required fields.
4. Click the footer connection summary and set protocol/IP/Port.
5. Save.

## 2. Workspace layout

The window is split into three main areas:
- Left panel: storage browser (folders + supported files).
- Center: editable PLC tag table.
- Bottom: connection summary and tag count.

A centered modal overlay appears when editing connection settings.

## 3. File operations

### Open files

You can open:
- XML files (.xml)
- CSV documentation files (.csv)
- XML TAR bundles (.xml.tar)

UTICOR field deployment accepts XML only. Export `.xml` for field use.
Use `.csv` for documentation and review workflows.

Open methods:
- File picker
- Launch argument: scripts/run.sh /path/to/file.xml
- Click a file in the storage panel

### Save and Save As

- Cmd/Ctrl+S saves to the current file.
- Cmd/Ctrl+Shift+S saves as a new file.
- Save format is inferred from extension (.xml, .csv, .xml.tar).

Important:
- Incomplete rows must be corrected or removed before save can complete.
- Save rebuilds the canonical router tag region and regenerated preload rows.

## 4. Table editing (daily use)

### Cell editing

- Text fields open as inline editors.
- Dropdown fields open a selectable options menu.
- Enter commits text or applies dropdown selection.
- Escape cancels the active field edit.
- Clicking another editable field commits current valid value and opens the next field.

### Change visibility

- Changed fields are outlined.
- Hover changed fields to see original/saved value context.
- Datatype cells include richer tooltip details about datatype/encode and repairs.

### Undo and redo

- Cmd/Ctrl+Z: Undo
- Cmd/Ctrl+Shift+Z: Redo

Undo is intentionally bounded by the saved/imported baseline so previously
saved values are not accidentally erased back to blank.

### Illegal character behavior

- Tag Group and Tag Name reject whitespace while typing.
- Address Start rejects non-digit input while typing.
- Illegal input attempts trigger a short red-dotted flash as feedback.
- If an illegal character is pressed before a text editor opens, the editor
  does not open and current row text is unchanged.

## 5. Row selection and row operations

### Selection

- Click: single row
- Shift-click: range selection
- Cmd/Ctrl-click: toggle row
- Cmd/Ctrl+A: select all visible rows
- Click empty table space: clear selection

### Row actions

- Right-click row: Copy, Insert, Cut, Delete
- Right-click true empty table area: Add Row
- Cmd/Ctrl +: insert below selection, or append at bottom with no selection
- Delete: remove selected rows

### Drag and reorder

- Drag one or multiple selected rows to reorder.
- Escape cancels active drag.
- Releasing outside valid table target does not apply reorder.

Escape behavior when rows are selected:
- Press Escape once to clear selected rows.

## 6. Connection configuration

Click the bottom-left connection summary to open the connection editor.

Protocol behavior:
- TCP: IP address + Port fields are used.
- RTU: IP address + Port fields are used, but remain visible for documentation purposes.

Validation rules:
- IP must be four numeric octets, each 0-255.
- Port must be numeric.

Apply writes a tracked edit. Cancel closes without applying changes.

## 7. Validation and issues

Alchemy emphasizes actionable validation:
- Required-field empties are highlighted.
- Tag Group and Tag Name cannot contain whitespace.
- Address Start must be digits only.
- Address overlap conflicts are flagged by occupied range, not just equal start address.
- Non-default scaling and datatype irregularities remain visible for review.

Title-shell issue state:
- Healthy state when no actionable issues exist.
- Warning state when issues require attention.

## 8. Storage panel workflows

### Navigation

- Click folders to enter.
- Use Back/Forward navigation.
- Click files to load.

### Context actions

Row menu:
- Show in Finder
- Rename
- Delete

Background menu:
- Show in Finder
- New Folder

### Drag and drop

- Internal drag: moves files/folders inside storage root.
- External drag: copies dropped files into storage root/folder.

## 9. Settings page

The Settings page controls environment-level preferences:
- Interface theme: cycles between System, Dark, and Light.
- Alchemy Root: sets the storage-panel root folder path.

Diagnostics visibility policy:
- File-panel diagnostic color dots are always on.
- They cannot be disabled from Settings.

## 10. Export and preload behavior

On save, preloads are deterministic:
- Imported preload rows are removed from editable set.
- Coil (01) and holding-register (03) groups are calculated separately.
- Only contiguous occupied ranges are grouped.
- A preload group is emitted only when it covers at least two tags.
- Isolated tags use PRELOAD="none".

This keeps output consistent and avoids accidental oversized preload regions.

## 11. Lesser-known but important features

These are easy to miss but useful:
- File rename preserves .xml extension even when hidden in panel labels.
- Copy/paste row data is aligned to PLC Tag List tab-separated layout.
- Context menus are single-instance to prevent stale stacked menus.
- Escape is a broad cancel key: active field, open menus, drag, and pending cut markers.
- Empty startup workspace is not dirty until a real mutation occurs.

## 12. Keyboard reference

| Shortcut | Action |
| --- | --- |
| Cmd/Ctrl+S | Save |
| Cmd/Ctrl+Shift+S | Save As |
| Cmd/Ctrl+Z | Undo |
| Cmd/Ctrl+Shift+Z | Redo |
| Cmd/Ctrl+A | Select all rows |
| Cmd/Ctrl + | Add/insert row |
| Cmd/Ctrl+C | Copy selected rows/text |
| Cmd/Ctrl+X | Cut selected rows/text |
| Cmd/Ctrl+V | Paste rows/text |
| Cmd/Ctrl+Delete | Clear active text field |
| Delete | Delete selected rows |
| Escape | Cancel current operation |

## 13. Troubleshooting quick guide

### Save fails

Check for:
- Incomplete rows
- Invalid addresses
- Connection field validation errors

### Row will not reorder

Check for:
- Drop target outside valid table area
- Drag canceled with Escape

### Data looks different after save

This is expected when imported data is normalized to canonical output and
preloads are regenerated.

## 14. Engineering references

- docs/ALCHEMY_BACKEND_GUIDE.md
- docs/ALCHEMY_FRONTEND_GUIDE.md
- docs/ALCHEMY_V1_REFERENCE.md
