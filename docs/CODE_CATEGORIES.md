# Code Categories

This file maps the app into practical categories so cleanup and standardization can happen safely.

## 1) Home Page
- Purpose: landing page, import/create/open recent docs.
- Main files:
  - `app/views/documents/index.html.erb`
  - `app/javascript/controllers/recent_docs_controller.js`
  - `app/javascript/controllers/import_form_controller.js`
  - `app/assets/stylesheets/application.css` (home section)

## 2) Table (Rows, Columns, Validation)
- Purpose: editable tag grid, validation, row operations, reordering.
- Main files:
  - `app/views/documents/edit.html.erb` (table markup and form fields)
  - `app/javascript/controllers/tag_table_controller.js`
  - `app/javascript/controllers/data_type_picker_controller.js`
  - `app/assets/stylesheets/application.css` (table and popup styles)

## 3) Editing Actions (Toolbar / Buttons / Export)
- Purpose: add row, select mode, lock/unlock, home guard, export flow.
- Main files:
  - `app/views/documents/edit.html.erb` (toolbar buttons)
  - `app/javascript/controllers/workspace_lock_controller.js`
  - `app/javascript/controllers/tag_table_controller.js` (`requireTitleBeforeHome`, `saveThenExport`)
  - `app/controllers/documents_controller.rb` (`export`)

## 4) Status System
- Purpose: status text, simple/detailed toggle, row highlight feedback.
- Main files:
  - `app/javascript/controllers/tag_table_controller.js` (`setStatus`, `handleStatusClick`)
  - `app/assets/stylesheets/application.css` (`.loaded-status`, `.status-detailed`, `.row-status-highlight`)

## 5) Save Pipeline
- Purpose: delta saves for single-field edits; full saves for bulk row ops.
- Main files:
  - `app/javascript/controllers/tag_table_controller.js` (`saveForm` + delta builders)
  - `app/javascript/controllers/data_type_picker_controller.js` (delta event detail)
  - `app/controllers/documents_controller.rb` (`update`, `handle_delta_update`)

## 6) Import/Export Mapping Rules
- Purpose: strict datatype mapping and XML conversion.
- Main files:
  - `app/services/tag_xml.rb` (`DataTypeMapper`, `Parser`, `Exporter`)
  - `app/controllers/documents_controller.rb` (`create`, `export`)

## 7) Recommended Cleanup Order (No Behavior Change)
1. Add comments and section headers only.
2. Extract tiny helper functions (pure refactors).
3. Group related methods within existing files.
4. Add/expand tests after each small refactor.
5. Only then consider splitting large controllers into smaller modules.

## 8) Guardrails for Safe Refactor
- Keep all public method names and event bindings unchanged first.
- Keep all `data-*` attributes and Stimulus target names unchanged first.
- Run quick manual checks after each small edit:
  - edit a text cell and press Enter/Tab
  - change a dropdown (including same-value reselect)
  - open/commit/cancel Data Type popup
  - drag rows and verify duplicate highlighting
  - export XML
