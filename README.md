# Alchemy

Alchemy is a standalone desktop editor for creating, repairing, and validating
UTICOR Modbus router documents.

It is designed for operator-speed table editing with engineering-grade save
rules, deterministic preload regeneration, and clear validation feedback.

## Highlights

- Direct row and cell editing for fast table updates.
- Practical keyboard shortcuts for row and cell operations.
- Deterministic XML save pipeline with preload repair and regeneration.
- Storage panel with root-folder selection, file navigation, rename, drag/drop, and Finder integration.
- Inline diagnostics for actionable issues such as missing fields and address overlap.

## Platform and Requirements

- macOS (Apple Silicon)
- .NET SDK 10.x (version pinned in global.json)

## Quick Start

```bash
./scripts/setup.sh
./scripts/run.sh
```

Open a specific file on launch:

```bash
./scripts/run.sh /path/to/document.xml
```

## Verification

```bash
./scripts/verify.sh
```

## Solution Structure

- src/Alchemy: Main Avalonia desktop application, table editor, XML pipeline, panel workflows.
- src/Alchemy.Kit: Shared UI and macOS integration helpers.
- src/Alchemy.Core: UI-agnostic contracts and path safety rules.

## Documentation

- docs/ALCHEMY_USER_GUIDE.md: Operator usage and keyboard reference.
- docs/ALCHEMY_FRONTEND_GUIDE.md: UI architecture, interaction patterns, and visual composition.
- docs/ALCHEMY_BACKEND_GUIDE.md: Data model, parsing, validation, save contracts, and extension seams.
- docs/ALCHEMY_V1_REFERENCE.md: Historical changes and release reference context.

## Typical Development Workflow

1. Run setup once per environment with scripts/setup.sh.
2. Launch the app with scripts/run.sh.
3. Implement focused changes in src/Alchemy/AlchemyWindow.axaml.cs and paired UI in src/Alchemy/AlchemyWindow.axaml when needed.
4. Verify with scripts/verify.sh before publishing.

## Safety Notes

- Preserve save contract behavior unless intentionally changing XML semantics.
- Keep preload generation deterministic and test with mixed datalength rows.
