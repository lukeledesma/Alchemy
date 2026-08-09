# Alchemy Frontend Guide

Status: Authoritative UI guide

Last updated: 2026-08-08

Primary implementation:
- src/Alchemy/AlchemyWindow.axaml
- src/Alchemy/AlchemyWindow.axaml.cs
- src/Alchemy/AlchemyTitleShell.axaml(.cs)

## Purpose

This document explains how the Alchemy user interface is structured and how
interaction behavior is implemented. It is intended for engineers extending or
maintaining the desktop UX.

## Frontend Architecture

Alchemy uses Avalonia with a code-behind driven interaction model.

Core pieces:
- AlchemyWindow.axaml: layout, styles, named controls, and top-level visual composition.
- AlchemyWindow.axaml.cs: interaction controller (editing, selection, menus, drag/drop, modal flows).
- AlchemyTitleShell: title bar/status shell and command affordances.
- Alchemy.Kit: reusable controls and platform integration helpers.

Design intent:
- Keep user interactions fast and direct.
- Preserve table focus and keyboard continuity.
- Surface validation without interrupting flow.

## Window Layout Model

The main window is a three-zone editor shell:
- Left panel: storage tree for folders and supported files.
- Main table region: sortable header plus editable rows in synchronized scroll viewers.
- Footer connection region: connection summary with modal editor access.

Overlay model:
- A full-window connection editor overlay presents a centered modal card.
- Backdrop clicks are consumed so edits are never discarded silently.

## UI State Ownership

Primary UI state lives in AlchemyWindow.axaml.cs and is refreshed through
explicit render passes.

Important UI state categories:
- Selection: active row/cell identity and multi-select source index set.
- Editing: active text editor shell, active dropdown menu shell, edit baseline.
- Menus: active context menu references for safe close/reopen behavior.
- Drag operations: row and panel drag state, insertion target indicators.
- View filter/sort: visible rows, sort column/direction, issue filter mode.

Guideline:
- Keep render methods deterministic.
- Rebuild affected visuals from state rather than incremental mutation when risk is high.

## Table Interaction Contract

The table is always editable when a file/workspace is loaded.

Cell editing modes:
- Text fields: lightweight in-place TextBox editor.
- Option fields: context-menu dropdown shell.

Selection behavior:
- Click: single row selection.
- Shift-click: range selection.
- Command/Ctrl-click: toggle row in selection.

Navigation behavior:
- Arrow, Tab, Shift+Tab: row/cell movement respecting edit mode semantics.
- Enter in text field: commit field text.
- Enter in dropdown: apply highlighted option and close.

Undo behavior:
- Undo is bounded by saved/imported baseline values to avoid deleting stable source values by accident.

## Visual Language and Feedback

Feedback is designed to communicate state without blocking work.

Visual signals:
- Dashed outline: edited or flagged values.
- Muted red outline: invalid actionable input.
- Conflict tinting: overlapping address ranges.
- Tooltip detail: original values and datatype metadata.

Panel presentation:
- Idle rows remain visually quiet.
- Hover and active selection provide emphasis.

## Connection Editor UX

The footer summary is the entry point for connection settings.

Modal features:
- Protocol selection (TCP/RTU).
- Validated IP Address and Port fields.
- Explicit Apply/Cancel actions.
- Per-field Escape restore behavior while focused.

Rules:
- Apply commits through standard change pipeline and undo snapshot.
- Cancel exits without mutation.

## Storage Panel UX

Panel supports both filesystem navigation and safe file operations.

Capabilities:
- Folder navigation with Back/Forward history.
- File open, rename, delete, show in Finder.
- Background actions such as New Folder.
- Internal drag move and external drag copy.

Conventions:
- Supported document extensions remain preserved on rename.
- Unsupported/hidden filesystem entries are filtered from UI.

## Accessibility and Input Consistency

Input behavior should remain consistent across mouse, trackpad, and keyboard.

Standards:
- Focus must remain predictable during edit and menu transitions.
- Context-menu and overlay handlers must not interfere with normal editor focus.
- Command and Control modifier paths should remain parity-mapped.

## Extension Seams

When adding frontend features, prefer these seams:
- New cell behavior: CreateEditCellShell and editor key handlers.
- New row visuals: CreateRow and AddCell pipelines.
- New modal flow: overlay + card pattern used by connection editor.
- New title-level command: AlchemyTitleShell event wiring.

Avoid:
- Bypassing baseline comparison pathways for edited-state visuals.
- Directly mutating generated row visuals without state reconciliation.

## Verification Checklist for UI Changes

Use this checklist after frontend changes:
- Build passes via scripts/verify.sh.
- Selection and edit navigation still match keyboard reference.
- Dropdown and text editing commit/cancel semantics are unchanged.
- Context menus close/reopen cleanly with no focus traps.
- Connection modal opens, validates, applies, and cancels correctly.
- Drag/drop row reorder and panel drag operations still behave correctly.

## Related Documents

- docs/ALCHEMY_USER_GUIDE.md
- docs/ALCHEMY_BACKEND_GUIDE.md
- docs/ALCHEMY_V1_REFERENCE.md
