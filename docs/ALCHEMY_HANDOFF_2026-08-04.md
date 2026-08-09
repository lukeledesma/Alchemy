# Alchemy Engineering Handoff (2026-08-04)

> Historical snapshot: this document is retained for the state of Alchemy on
> 2026-08-04. It is not the current implementation contract. Continue from
> [ALCHEMY_BACKEND_GUIDE.md](ALCHEMY_BACKEND_GUIDE.md).

This document is the top-to-bottom engineering handoff for Alchemy as it exists today, including behavior contracts, data flow, UI semantics, and external reference assets.

## 1. Scope

Covers:
- Alchemy runtime surfaces and user interaction model.
- XML parsing and table rendering pipeline.
- Datatype dictionary and exception-repair model.
- Datalength risk signaling behavior.
- Storage panel diagnostics and hover UX.
- Context menu and Finder integration.
- File-level references for maintenance.

## 2. Runtime Surfaces

Alchemy has two major functional surfaces:

1. Left storage panel
- Navigates the configured storage root.
- Supports folder navigation, history back/forward, rename/delete/new folder.
- Supports drag/drop (internal and external) with path safety rules.
- Shows compact diagnostics dots for XML files.

2. Main table area
- Renders parsed XML endpoint rows.
- Supports sorting, selection, keyboard navigation, and clipboard copy.
- Applies row/cell diagnostics (conflicts, unknowns, repaired mappings, scaling anomalies, datalength risk).

## 3. Data Sources and Contracts

## 3.1 Internal dictionary used by app
- File: `src/Alchemy/Data/Uticor Dictionary.txt`
- Purpose: maps UTICOR datatype/encode pairs to human-readable PLC data types.
- Includes:
  - Valid canonical pairs.
  - Explicit exception pairs treated as repairable.
  - Functional-code notes for boolean interpretation.

## 3.2 External references imported for this handoff
- Folder: `docs/alchemy-reference-assets`
- Files:
  - `UTICOR_DICTIONARY_2026-08-04.rtf`
  - `PLCTaglistExcelMacro_2026-08-04.bas`
  - `PLC_Tag_List_2026-08-04.xlsm`
- Purpose:
  - Preserve source-of-truth artifacts used to validate mappings and output behavior.
  - Keep reproducible references for future maintenance and auditing.

## 4. XML Parse and Row Model Pipeline

High-level sequence:

1. User opens/selects XML.
2. XML blocks are parsed into row candidates.
3. Candidate fields extracted include (at minimum):
- NODEID
- TYPE
- ADDRSTART
- DATALENGTH
- DATATYPE
- ENCODE
- EXPR
- VERIFY
4. Row is classified:
- preload vs normal tag
- resolved datatype vs unknown
- exception-repaired vs canonical
- address conflict participation
- scaling default vs unusual
5. Rows become visual table items with tooltips and brushes.

Main implementation:
- `src/Alchemy/AlchemyWindow.axaml.cs`

## 5. Datatype Resolution Semantics

Resolution is dictionary-driven:
- `DATATYPE + ENCODE` is normalized and looked up.
- Canonical pairs map directly.
- Exception pairs are flagged as repaired mappings.
- Unrecognized pairs become `Unknown`.

Repair behavior:
- Table `Data Type` cell uses repair/unknown/mismatch brush logic.
- Tooltip separates current UTICOR values from repaired target values when applicable.

## 6. Datalength Risk Signaling

Risk intent:
- Signal transition drift between source XML `DATALENGTH` and inferred Excel output datalength.

Rules:

1. Preloads are excluded from mismatch risk.
2. Source `DATALENGTH` is parsed numerically.
3. Inferred output length is derived from mapped data type contract:
- Contains `DINT` or `REAL` -> `2`
- `BOOL (Bit of INT)` display -> `1[bit]` in tooltip display path
- Otherwise -> `1`
4. Mismatch (`source != inferred output`) is treated as high-priority risk.

Visual semantics:
- DataType cell text: red when mismatch exists.
- Tooltip:
  - current datatype/encode lines (neutral)
  - repaired lines (blue, changed fields only)
  - datalength risk line (red)

## 7. Table Interaction and Sort Contracts

- Sort cycle per column: asc -> desc -> default.
- Preload rows grouped separately from standard rows.
- Address sorting is numeric and scope-aware.
- Row selection:
  - single select
  - range select (shift)
  - additive toggle (cmd/ctrl)
  - copy selected rows (cmd/ctrl+c)

## 8. Panel Diagnostics UX (Latest)

Diagnostics categories (file-level):
- Address conflicts
- Unknown data types
- Repaired data types
- Unusual scaling values

Latest hover behavior:
- No per-dot tooltip requirement.
- Hovering diagnostics area opens one stacked tooltip with all active categories.
- Hover area is intentionally larger than dot glyphs for reliable targeting.
- Visual capsule border removed after tuning; hitbox remains expanded.

## 9. Finder and Open Actions

Storage context menu additions:
- `Open`
- `Show in Finder`

Implemented in both:
- Main Nexus storage tree context menus.
- Alchemy panel context menus.

Shared helper:
- `src/Alchemy/FileManagerReveal.cs`

## 10. Key Files and Responsibilities

Core Alchemy files:
- `src/Alchemy/AlchemyWindow.axaml` (layout)
- `src/Alchemy/AlchemyWindow.axaml.cs` (logic)
- `src/Alchemy/AlchemyTitleShell.axaml` (title controls, filters)
- `src/Alchemy/AlchemyTitleShell.axaml.cs` (title shell logic)
- `src/Alchemy/AlchemyDataCatalog.cs` (dictionary parser + mapping)
- `src/Alchemy/Data/Uticor Dictionary.txt` (mapping source)

Theme resources:
- `src/Alchemy/App.axaml`
- `src/Alchemy/AlchemyApp.axaml`

Standalone Alchemy linkage:
- `src/Alchemy/Alchemy.csproj`

## 11. Verification Checklist

1. Open an XML file with mixed canonical/exception/unknown rows.
2. Confirm dots appear in panel for categories present.
3. Hover diagnostics area once (not each dot) and verify stacked counts.
4. Confirm expanded hover area is easy to trigger.
5. Confirm DataType mismatch rows are red.
6. Open DataType tooltip and verify:
- current lines,
- repaired lines (blue, changed values only),
- datalength risk line (red) when mismatch exists.
7. Confirm preloads do not show mismatch risk even if widths differ.
8. Validate context menus show `Open` and `Show in Finder`.

## 12. Operational Notes

- Keep dictionary exceptions synchronized with UTICOR reference updates.
- Preserve dated snapshots in `docs/alchemy-reference-assets` when new source documents arrive.
- Maintain `docs/ALCHEMY_BACKEND_GUIDE.md` as the current engineering reference. Keep this file unchanged except for historical clarifications.
