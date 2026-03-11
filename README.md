# Alchemy

Alchemy is a Rails-based Modbus/Uticor XML tag-list editor.

Core flow:
1. Organize PLC tag-list files in folders.
2. Import XML (including `.xml.tar` and `.xml.tar.gz`) into an existing folder.
3. Edit rows in the workspace table.
4. Save (delta or full-save paths).
5. Export XML with preloads rebuilt from current records.

## Current V1 behavior

- Brand/title is `ALCHEMY` (no superscript suffix).
- Tab icon uses custom wizard favicon assets from `public/`.
- Organizer supports folder creation, per-folder import, per-folder new file, rename, and delete.
- Imports must target an existing folder (`parent_id` required).
- Legacy root-level fallback `Imported` auto-folder is removed.
- Data type popup includes bottom-row dynamic spacing behavior for last rows in workspace.

## Requirements

- Ruby `3.2.3` (see `.ruby-version`)
- PostgreSQL
- Bundler

If `pg` install fails on macOS/Homebrew:

```bash
gem install pg -- --with-pg-config=/opt/homebrew/bin/pg_config
```

## Setup

```bash
cd /path/to/Alchemy
bundle install
bin/rails db:create
bin/rails db:migrate
```

## Run

```bash
bin/rails server
```

Open `http://localhost:3000`.

## Test

Run full test suite:

```bash
bin/rails test
```

Focused integration/service checks used during recent updates:

```bash
bin/rails test test/integration/documents_import_test.rb test/services/tag_xml_exporter_test.rb
```

## Feature summary

### Home + Organizer

- Displays folders and file counts.
- Per-folder actions:
	- `Import` (AJAX multipart)
	- `New` (scaffold file)
	- `Rename`
	- `Delete`
- Unfiled file section appears when root files exist.
- Organizer actions are no longer tied to a home lock toggle.

### Workspace

- Editable columns:
	- Tag Group
	- Tag Name
	- Data Type
	- Address Start
	- Data Length
	- Scaling
	- Read/Write
- Toolbar includes add row, select mode, lock toggle, home, and export.
- Save pipeline supports delta updates and full updates.
- Validation and status highlighting are managed in Stimulus.

### Import

- Accepts:
	- `.xml`
	- `.xml.tar`
	- `.xml.tar.gz`
- For tar archives, XML content is extracted and parsed.
- Import requires destination folder (`parent_id`).
- Filename collision resolution uses finder-style numeric suffixing.

### Export

- Exports current records as XML.
- Rebuilds `Preload_Words_*` and `Preload_Bits_*` blocks.
- Preload `DATALENGTH` rules:
	- bits/coils use range delta to last address
	- words/registers use range delta plus one

## Key files

Backend:
- `app/controllers/documents_controller.rb`
- `app/models/document.rb`
- `app/services/tag_xml.rb`
- `app/services/document_storage_sync.rb`

Frontend:
- `app/views/documents/index.html.erb`
- `app/views/documents/_organizer.html.erb`
- `app/views/documents/edit.html.erb`
- `app/javascript/controllers/tag_table_controller.js`
- `app/javascript/controllers/data_type_picker_controller.js`
- `app/javascript/controllers/recent_docs_controller.js`

Styling:
- `app/assets/stylesheets/application.css`

## Storage model

- DB source of truth: `documents` table.
- Disk mirror root: `storage/tag_lists`.
- Folder/file renames, deletes, and writes sync through `DocumentStorageSync`.

## Notes for maintainers

- Keep `data-*` hooks stable when changing Stimulus-driven views.
- Prefer small, behavior-preserving edits.
- If touching import/export, validate with XML round-trip scenarios.
- Use `docs/CODE_CATEGORIES.md` as a feature ownership/refactor map.
