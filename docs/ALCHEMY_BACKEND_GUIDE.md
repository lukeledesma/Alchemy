# Alchemy Backend Guide

Status: Authoritative backend implementation guide

Last updated: 2026-08-08

Primary implementation: src/Alchemy/AlchemyWindow.axaml.cs

This guide focuses on backend correctness and efficiency for Alchemy V1.
It describes the runtime model, persistence contract, performance-sensitive
paths, invariants, and safe extension seams.

## 1. Backend mission and constraints

Alchemy backend guarantees:
1. Parse supported documents into a stable row model.
2. Exclude imported preload rows from editable table state.
3. Preserve saved/imported baseline values for comparison and bounded undo.
4. Validate actionable issues without silent destructive normalization.
5. Save canonical tag output with deterministic preload regeneration.

Non-goals:
- Byte-for-byte XML preservation.
- Lossless support of unrelated router constructs that are outside tag model scope.

## 2. Runtime composition

Projects:
- src/Alchemy: app runtime, row state, parser, save engine, panel workflows.
- src/Alchemy.Kit: UI/helper abstractions used by app runtime.
- src/Alchemy.Core: shared contracts and path safety helpers.

Hot backend surfaces:
- ParseTagRows
- LoadXmlContent and LoadAlchemyDocumentContent
- RefreshRows and row rebuild pipeline
- BuildEditedXmlContent
- CalculatePreloadSections
- ApplyCellEdit / snapshot lifecycle

## 3. Core model and state ownership

Primary row model:
- AlchemyTagRow (immutable record)

Identity strategy:
- SourceIndex is the stable UI identity key.
- SourceIndex is not Modbus address identity.

Primary collections:
- _allRows: authoritative editable rows
- _visibleRows: sorted/filtered projection
- _editBaselineRows: saved/imported baseline by SourceIndex
- _selectedSourceIndexes: selection independent of sort
- _undoEdits and _redoEdits: snapshot stacks
- _rowClipboard: row-level clipboard model

Efficiency note:
- Maintain immutable row replacement and targeted recalculation to prevent
  hidden coupling between selection, baseline state, and render state.

## 4. Data flow pipeline

### Load pipeline

1. Determine file format by extension.
2. Parse rows from XML or CSV path.
3. Detect and classify imported preload rows.
4. Annotate address conflicts.
5. Remove preload rows from editable set.
6. Rebuild baseline map.
7. Reset undo/redo and dirty state.
8. Refresh visible table projection.

### Edit pipeline

1. Open field editor (text or dropdown).
2. Validate and normalize user input.
3. Create immutable row replacement.
4. Re-annotate dependent diagnostics.
5. Push snapshot for undo/redo.
6. Refresh affected UI state and issue counts.

### Save pipeline

1. Reject or resolve incomplete rows.
2. Build canonical edited XML body.
3. Regenerate preload sections.
4. Persist by format (.xml, .csv, .xml.tar).
5. Reload saved content into runtime state.
6. Clear dirty and reset snapshot stacks.

Efficiency note:
- Save-reload is intentional and acts as a state normalization checkpoint.

## 5. Format boundaries and persistence

Supported formats:
- XML (.xml)
- CSV (.csv)
- XML TAR (.xml.tar)

Boundary behavior:
- Save format is inferred from target extension.
- XML TAR persistence writes XML payload through tar entry logic.
- CSV round-trip maps table columns explicitly.

Do not alter format routing without updating:
- GetSaveFormat
- LoadAlchemyDocumentContent
- ParseRowsForDocument
- SaveEditedXmlAsync

## 6. Parsing and normalization

Parser strategy:
- Tolerant, regex-oriented parsing for UTICOR export quirks.
- Supports quoted entry names and non-standard patterns found in field files.

Tag candidate extraction includes:
- TYPE
- NODEID
- ADDRSTART
- DATALENGTH
- DATATYPE
- ENCODE
- EXPR
- SUBSCRIBE
- VERIFY
- preload reference aliases

Preload detection is intentionally broad and includes legacy naming patterns.

Efficiency note:
- Keep regex passes bounded and avoid per-row repeated expensive scans over full document text.

## 7. Validation model

Required row completeness:
- Tag Group present and whitespace-free
- Tag Name present and whitespace-free
- Data Type present
- Address Start digits only
- Scaling, Read/Write, Update Data set

Diagnostics include:
- Required-field violations
- Address occupancy conflicts
- Datatype mismatch/unknown/exception conditions
- Scaling anomalies

Principle:
- Report and highlight; do not silently mutate imported identifiers.

## 8. Datatype resolution and repair

Dictionary source:
- src/Alchemy/Data/Uticor Dictionary.txt

Resolver responsibilities:
- Interpret datatype/encode pairs by register kind
- Normalize numeric code strings
- Distinguish canonical mappings vs reference-only sections

Repair semantics:
- Explicit datatype selection commits canonical pair and derived datalength.
- Imported exceptions remain visible until explicit user action resolves them.

## 9. Undo/redo and baseline contract

Baseline source:
- _editBaselineRows from last load/save

Snapshot source:
- AlchemyEditSnapshot before/after collections

Critical behavior:
- Text undo is bounded by baseline value to prevent accidental deletion
  beyond saved/imported state.
- Save-reload re-anchors baseline and clears stacks.

## 10. Preload generation algorithm

Entry point:
- BuildEditedXmlContent

Rules:
1. Remove preload rows from editable set.
2. Build coil and holding sections independently.
3. Build occupancy ranges using effective datalength.
4. Merge only directly contiguous ranges.
5. Emit preload only for ranges covering at least two tags.
6. Mark isolated tags with PRELOAD="none".

Generated preload defaults:
- Names: Preload_Words_START_END or Preload_Bits_START_END
- DATALENGTH: END - START + 1
- DATATYPE: 103
- ENCODE: 255
- VERIFY: 254

Efficiency note:
- Range merge is linear after sort; keep it single-pass.

## 11. XML generation contract

Canonical generated fields include:
- TYPE, DEVICEID, FUNCCODE, ADDRSTART, DATALENGTH
- NODEID, SERIAL, IP, PORT
- PRELOAD, VERIFY, DATATYPE, ENCODE, EXPR, SUBSCRIBE, POLL

Mapping highlights:
- Read+Write -> SUBSCRIBE on
- Read Only -> SUBSCRIBE off
- On Scan-Rate -> VERIFY 0
- On Change -> VERIFY 7
- Read-only scaling persists reciprocal EXPR where valid

Preservation behavior:
- When possible, non-tag XML entries are retained while recognized tag region is rewritten.

## 12. Storage-panel backend contracts

Storage root:
- Driven by settings store

Guaranteed behaviors:
- Hidden entries filtered
- Rename preserves expected file extension behavior for supported files
- Internal drag performs safe move checks
- External drag performs copy/import logic

Safety seams:
- StoragePathRules (src/Alchemy.Core/StoragePathRules.cs)
- Collision and containment checks in panel operations

## 13. Efficiency playbook (backend)

When optimizing, prioritize these wins:
1. Avoid unnecessary full RefreshRows calls in narrow edit paths.
2. Keep sort/filter projections separate from source row mutation.
3. Reuse parsed metadata where safe instead of reparsing unchanged buffers.
4. Avoid repeated large string allocations during save assembly.
5. Keep drag/selection operations O(n) over visible rows.

Anti-patterns to avoid:
- Mutating _allRows in-place without snapshot and conflict recomputation.
- Introducing hidden coupling between UI selection state and save state.
- Replacing tolerant parser with strict DOM parser without compatibility fixtures.

## 14. Extension checklists

### Add a new editable field

Update all of:
1. Field enum and row property mapping.
2. Display renderer and edit shell.
3. Validation and completeness checks.
4. Clipboard serialization.
5. Parse/import mapping.
6. Save/export mapping.
7. Baseline comparison tooltips/outlines.
8. Undo snapshot semantics.

### Add or change file format behavior

Update all of:
1. GetSaveFormat routing.
2. LoadAlchemyDocumentContent parse dispatch.
3. SaveEditedXmlAsync write path.
4. Round-trip verification fixtures for open/edit/save/reopen.

### Change preload rules

Update all of:
1. CalculatePreloadSections and assignment logic.
2. Generated preload entry defaults.
3. Conflict and occupancy assumptions.
4. User-facing docs and manual expectations.

## 15. Verification checklist (backend changes)

Run:
- scripts/setup.sh
- scripts/run.sh
- scripts/verify.sh

Manual checks:
- Open XML with legacy preload names.
- Edit rows including multi-register datatypes.
- Validate address conflict highlighting.
- Save and reopen; confirm deterministic preload generation.
- Save as .csv and .xml.tar; reopen and verify state parity.
- Confirm undo/redo boundary behavior after save.

## 16. Known V1 limits and future opportunities

Current limits:
- No automated unit/integration test suite.
- Monolithic interaction and backend logic concentrated in one main window file.

Recommended V2 direction:
- Extract parser/save/validation services into testable units.
- Add fixture-driven round-trip tests for XML/CSV/XML.TAR.
- Add targeted performance probes around parse/save and row refresh paths.

## 17. Related references

- docs/ALCHEMY_FRONTEND_GUIDE.md
- docs/ALCHEMY_USER_GUIDE.md
- docs/ALCHEMY_V1_REFERENCE.md
