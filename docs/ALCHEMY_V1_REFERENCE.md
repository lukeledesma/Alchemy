# Alchemy V1 - Comprehensive Application Reference

This document is the canonical technical reference for Alchemy V1.
It covers architecture, data flow, backend and frontend layout, feature behavior,
and operational conventions as currently implemented.

## 1. Product Overview

Alchemy is a Rails application for managing and editing Modbus/Uticor XML tag lists.

Primary user workflow:
1. Organize files into folders from the home organizer.
2. Import XML (`.xml`, `.xml.tar`, `.xml.tar.gz`) into an existing folder.
3. Open a file in workspace and edit table rows/metadata.
4. Save changes (delta or full save pipeline).
5. Export XML with rebuilt preload sections.

Branding and shell:
- App title and metadata are `ALCHEMY`.
- Browser tab icon uses wizard favicon assets in `public/`.

## 2. Stack and Runtime

Backend:
- Ruby on Rails 8.x
- PostgreSQL
- Puma

Frontend:
- Server-rendered ERB views
- Stimulus controllers (via importmap)
- CSS in `app/assets/stylesheets/application.css`

Storage:
- Primary source of truth: database (`documents` table)
- Filesystem mirror: `storage/tag_lists`

## 3. Directory Layout (High Value Areas)

Core app code:
- `app/controllers/documents_controller.rb`
- `app/models/document.rb`
- `app/services/tag_xml.rb`
- `app/services/document_storage_sync.rb`

Views:
- `app/views/layouts/application.html.erb`
- `app/views/documents/index.html.erb`
- `app/views/documents/_organizer.html.erb`
- `app/views/documents/_folder_file_list.html.erb`
- `app/views/documents/edit.html.erb`

Stimulus controllers:
- `app/javascript/controllers/recent_docs_controller.js`
- `app/javascript/controllers/folders_controller.js`
- `app/javascript/controllers/rename_controller.js`
- `app/javascript/controllers/import_form_controller.js`
- `app/javascript/controllers/tag_table_controller.js`
- `app/javascript/controllers/workspace_lock_controller.js`
- `app/javascript/controllers/data_type_picker_controller.js`

Public icon assets:
- `public/favicon-32.png`
- `public/favicon.png`
- `public/icon.png`
- `public/icon.svg`

Tests:
- `test/integration/documents_import_test.rb`
- `test/services/tag_xml_exporter_test.rb`

## 4. Data Model and Persistence

### Document model

`Document` is used for both folders and files.

Important fields:
- `is_folder` (boolean)
- `parent_id` (folder/file hierarchy)
- `storage_path` (relative mirror path)
- `metadata_filename`
- `metadata_ip`
- `metadata_protocol`
- `records` (array of row hashes)
- `new_untitled_placeholder`

Associations:
- `belongs_to :parent, class_name: "Document", optional: true`
- `has_many :children, class_name: "Document", foreign_key: :parent_id`

Scopes:
- `folders`
- `files`

Behavior:
- Folder records normalize to empty records and no connection metadata.
- File records normalize filename and defaults.
- Validation enforces `records` as array and folder parent constraints.

## 5. Routing and Endpoints

Primary routes from `config/routes.rb`:
- `GET /` -> `documents#index`
- `POST /documents` -> `documents#create` (import / folder create branch)
- `PATCH /documents/:id` -> `documents#update`
- `DELETE /documents/:id` -> `documents#destroy`

Custom endpoints:
- `POST /documents/create_root_folder`
- `GET /documents/organizer_fragment`
- `POST /documents/:id/create_file`
- `PATCH /documents/:id/rename`
- `GET /documents/:id/export`
- `GET /documents/:id/file_list`

## 6. Home + Organizer UI Behavior

### Visual structure

Home uses organizer-first UX:
- Folder rows are listed with counts.
- Folder can expand to reveal contained PLC files.
- Folder action row supports Import/New where folder is writable.
- File rows expose open/rename/delete actions.

### Interaction behavior

Implemented by `recent_docs_controller.js`, `folders_controller.js`, `rename_controller.js`, `import_form_controller.js`.

Capabilities:
- Async organizer refresh after mutations.
- Folder expand/collapse.
- Inline rename with optimistic UX.
- Delete with animation + empty-state recalculation.
- Import form auto-submit for selected file.

Important current rule:
- Organizer actions are not lock-gated by any home-level lock controller.

## 7. Workspace UI Behavior

Workspace is rendered from `app/views/documents/edit.html.erb`.

Header controls:
- Title
- Protocol/IP metadata
- Add row
- Select mode
- Lock toggle
- Home
- Export

Table columns:
- Tag Group
- Tag Name
- Data Type
- Address Start
- Data Length
- Scaling
- Read/Write

Table behavior highlights:
- Hidden row template cloning for new rows.
- Sorting links in header.
- Validation and duplicate checks in table controller.
- Status line with simple/detailed feedback and row/field flash highlighting.

### Data type popup and bottom spacing behavior

`data_type_picker_controller.js` manages popup open/close, mapping, and per-row behavior.

Current implemented behavior:
- Popup spacing extension only applies to last rows where needed.
- Spacing "climbs" by row position in bottom region.
- Baseline gap is tuned for visual alignment.
- Scroll jitter fixes are in place (position updates and spacing updates are separated to avoid thrash).

## 8. Import Pipeline

Entry: `DocumentsController#create`

File support:
- `.xml`
- `.xml.tar`
- `.xml.tar.gz`

Archive import behavior:
- Tar archives are extracted to temp dir.
- First XML is selected and copied into a temp file for parser usage.

Critical rule:
- Import requires a valid destination folder via `parent_id`.
- Root-level auto-`Imported` fallback creation is removed.

Error handling:
- Missing file -> `422`/alert
- Missing destination folder -> `422`/alert
- Parse/import failure -> `422`/alert

## 9. Export Pipeline

Entry: `DocumentsController#export`

Exporter: `TagXml::Exporter`

Behavior:
- Generates XML by string concatenation.
- Rebuilds `Preload_Words_*` and `Preload_Bits_*` sections from current records.
- Resolves preload linkage per row.

Preload length rules (current):
- Bits/coils: preload data length uses range delta to last address.
- Words/registers: preload data length uses range delta plus one.

## 10. Save Pipeline (Delta + Full)

Entry: `DocumentsController#update`

Two paths:
- Delta update for targeted cell/field edits.
- Full form save for bulk operations.

Key constants:
- `RECORD_KEYS`
- `RAW_PRESERVE_KEYS`
- `METADATA_UPDATE_KEYS`

Outcomes:
- Saves DB records.
- Syncs storage mirror.
- Returns `204 No Content` for JS save flow.

## 11. Storage Sync and Naming Rules

Service: `DocumentStorageSync`

Responsibilities:
- Ensure folder paths exist.
- Write scaffold XML for new files.
- Sync file XML for non-empty records.
- Purge file/folder paths on delete.
- Rename with collision handling.

Naming behavior:
- Finder-style numeric suffix filling is used for collision resolution.
- Import names normalized by `resolve_import_filename` in target folder context.

## 12. Data Type and Mapping Domain

Service: `TagXml::DataTypeMapper`

Capabilities:
- Maps `(datatype, encode)` pairs to UI labels.
- Unknown pairs map to `Unique`.
- Provides reverse export codes.
- Provides Uticor code labels.

Related mappers:
- `ScalingMapper`
- `ReadWriteMapper`

Parser behavior:
- Normalizes incoming XML structure quirks.
- Skips `Preload_*` nodes on import.
- Preserves raw fields (`_raw_datatype`, `_raw_encode`, `_raw_verify`) where available.

## 13. Frontend-Backend Contract Notes

These contracts are sensitive and should be preserved unless intentionally refactored end-to-end:

- Table input names follow `records[index][Field Name]`.
- Hidden/raw fields for datatype popup are required for preserving exact code values.
- Organizer row `data-*` attributes are consumed by Stimulus controllers.
- Delta update payload shape must match controller expectations.

## 14. Security and Request Conventions

- CSRF tokens are used for mutating requests.
- Mutating XHR paths return useful `422` messages for UI feedback.
- Controller-side filtering and key allow-lists protect update/import paths.

## 15. Testing and Verification

Current focused tests:
- `documents_import_test.rb`
  - verifies `.xml.tar.gz` import
  - verifies destination-folder requirement
- `tag_xml_exporter_test.rb`
  - verifies preload length/address behavior

Recommended smoke checks after non-trivial changes:
1. Import into folder.
2. Create new file in folder.
3. Rename/delete folder and file.
4. Edit workspace rows and save.
5. Export and validate preload structure.

## 16. Operational Notes

- App currently assumes local filesystem access for storage mirror under `storage/tag_lists`.
- Browser favicon caching can be sticky; cache-busting query params are used in layout links.
- Existing local data in DB/filesystem can influence organizer contents independent of code behavior.

## 17. Current Branding and Icon Wiring

Brand:
- `ALCHEMY` (tab title, metadata, visible title)

Favicon links in layout:
- shortcut icon -> `/favicon-32.png` (cache-busted)
- icon 32x32 -> `/favicon-32.png` (cache-busted)
- generic icon -> `/favicon.png` (cache-busted)
- fallback icon -> `/icon.png` (cache-busted)
- apple touch icon -> `/icon.png`

## 18. Maintenance Guidance

- Prefer behavior-preserving changes and incremental patches.
- When editing a controller + view interaction, update both sides in the same change.
- Re-run focused tests after import/export or popup behavior modifications.
- Keep this file updated as the single docs reference for V1.
