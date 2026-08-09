# Alchemy Complete Engineering Handoff (2026-08-03)

> Historical snapshot: use [ALCHEMY_BACKEND_GUIDE.md](ALCHEMY_BACKEND_GUIDE.md)
> for current Alchemy architecture and behavior. This document remains useful
> for earlier Alchemy design history.

This handoff captures everything achieved across Nexus and Alchemy during the iterative build-out and stabilization cycle, from initial foundation through current state.

It is intended to be the high-detail project transfer artifact for any engineer taking ownership.

## 1. Scope and Intent

This document includes:
- Product and architecture milestones.
- Behavioral contracts implemented in Nexus and Alchemy.
- Drag/drop, rename, sorting, parser, and title-shell details.
- Build and developer workflow hardening.
- Repository size reduction strategy and outcomes.
- Current known limitations and next recommended work.

This document also includes in-progress, uncommitted deltas currently in the working tree as of 2026-08-03.

## 2. High-Level Outcome

Alchemy is now a reliable launcher and workspace shell with a significantly leaner development footprint, stronger behavior consistency, and better architectural boundaries.

Alchemy now has:
- Stable panel navigation and drag/drop model.
- Host drag history-hover navigation.
- Deterministic sorting and conflict display behavior.
- Better dictionary handling and typed mapping controls.
- Connection metadata displayed in title shell from XML (TYPE, IP, PORT).

Nexus now has:
- Clear root-target drag semantics.
- Side-panel storage target integration.
- Better storage state rendering and consistency.

Developer experience now includes:
- Scripted fast path for daily setup/run.
- Release verification separated from daily loop.
- Cleanup flow for generated artifacts.
- Retried restore behavior for transient NuGet failures.
- Cache behavior controls to avoid workspace bloat.

## 3. Timeline of Major Milestones

Chronological summary based on repository history and session-delivered changes.

### 3.1 Foundation
- Initial project scaffolding and runtime composition completed.
- Multi-project split established:
  - Alchemy
  - Alchemy.Core
  - Alchemy.Kit
  - tests/Alchemy.Tests

Representative commit:
- 7109fae (2026-07-29) Initial Alchemy foundation

### 3.2 Recovery and reproducibility hardening
- Recovery-first docs and reproducible setup flow added.
- Stronger developer onboarding and machine rebuild guidance.

Representative commit:
- 5b1864b (2026-07-30) Add reproducible developer setup and recovery guide

### 3.3 Alchemy functional expansion
- Alchemy table and panel behavior deepened:
  - conflict workflow
  - dictionary exceptions
  - launch dedupe updates
  - UI cleanup and stability

Representative commits:
- 49d8639 (2026-07-30)
- b714db8 (2026-07-30)

### 3.4 Cross-surface interaction consistency
- Behavior parity work across Nexus and Alchemy:
  - folder dimming model
  - rename visibility override
  - drag/drop semantics alignment
  - context menu consistency

Representative commits:
- 437611d (2026-07-31)
- 67d0b25 (2026-07-31)

### 3.5 Drag/drop UX standardization and docs refresh
- Root and folder target models clarified.
- Alchemy drag hover and panel-level targets improved.
- Documentation expanded to preserve contracts.

Representative commit:
- 8af6d50 (2026-08-01)

### 3.6 Architecture and duplication cleanup
- External drop helper extraction.
- Centralized color tokenization.
- Folder-state cache optimization.
- Shared path containment helper.

Representative commits:
- 087686c (2026-08-01)
- d924372 (2026-08-01)
- f46e063 (2026-08-01)

### 3.7 Script reliability and deploy flow hardening
- Restore retry logic added to run/setup flows.
- GitHub deploy helper script added.

Representative commit:
- 88b10db (2026-08-01)

### 3.8 Current working-tree (post-commit) major deltas
- Runtime/build footprint drastically reduced.
- Core/UI boundary further decoupled.
- Alchemy folder drag support expanded.
- Alchemy title-shell connection metadata displayed from XML.

These changes are reflected in the working tree and validated by local builds.

## 4. Architecture: Current State

## 4.1 Project responsibilities

### Alchemy
- Main UX surfaces and interactions.
- Nexus workspace storage UI.
- Alchemy tool window and parser/table behavior.
- Avalonia-specific window adaptation layer.

### Alchemy.Core
- Tool contract and launch lifecycle abstractions.
- Registry and launch routing.
- Shared path movement/containment rules.
- No longer requires direct Avalonia package dependency for tool-window abstraction.

### Alchemy.Kit
- Shared UI controls and reusable behaviors.
- Side-panel row and icon button primitives.

### Tests
- Storage path move rules validated in focused tests.
- Tests shifted to target Core contracts where possible.

## 4.2 Key boundary improvements

Implemented:
- Core tool window abstraction introduced:
  - IToolWindow
- ITool now returns IToolWindow instead of Avalonia Window.
- AvaloniaToolWindow adapter in App bridges framework window to Core contract.
- Alchemy.Core package dependency on Avalonia removed.

Impact:
- Cleaner separation of application concerns.
- Better long-term portability and testability of Core contracts.

## 5. Nexus (MainWindow) Achievements

## 5.1 Storage tree and state behavior
- Tree rendering and row reconciliation improved.
- Folder content state classes used for visual semantics:
  - HasFiles
  - FolderOnly
  - Empty
- Rename mode override ensures readability during edit.

## 5.2 Drag/drop semantics
- Internal drag/drop supports row-level folder targeting and root-level targeting.
- Root target clarity improved via dedicated outline and side panel storage-target feedback.
- External drops align with internal targeting semantics.

## 5.3 Workspace state persistence cleanup
- Expanded-folder restoration, save ordering, and path remap logic extracted into dedicated helper:
  - StorageWorkspaceState

Benefits:
- Reduced MainWindow responsibility concentration.
- More explicit, testable state policy.

## 6. Alchemy Achievements

## 6.1 Panel browser and navigation
- Left panel storage browser stabilized with:
  - folder navigation
  - back/forward history
  - context actions
  - external/internal drop handling

## 6.2 Drag/drop behavior

### Internal panel drag
- Original behavior was file-only move.
- Extended to item-aware move (files and folders).
- Validation now uses shared path movement rules.
- Invalid targets no longer show as droppable in internal drag path.
- Folder drag visual and functionality gate fixed by allowing drag start on Directory.Exists(path).

### External host drag
- Host file drop to folder/current-level targets supported.
- History-hover navigation (back/forward after hover delay) supported during external drag.

## 6.3 Move safety and state remapping
- Move path now remaps in-memory panel references when folder paths shift:
  - current path
  - active file path
  - rename path
  - back history
  - forward history

This prevents stale path pointers after folder moves.

## 6.4 XML parser and table behavior
- Tag row extraction from XML blocks with TYPE/NODEID presence filtering.
- Datatype/encode resolution integrated with dictionary and exception handling.
- Preload detection and grouping rules enforced.
- Conflict annotation by address/register scope implemented.
- Sorting behavior (including datatype precedence and numeric address ordering) stabilized.

## 6.5 Selection and clipboard
- Multi-select behavior and copy export maintained with keyboard shortcuts.
- Active row and visible-row interactions refined.

## 6.6 Title shell metadata enhancement
- Connection metadata now parsed from XML and displayed in title shell:
  - TYPE
  - IP
  - PORT
- Display format updated per UX request:
  - TCP or RTU only (plain)
  - IP: value
  - Port: value
  - Divider separators restored

Example:
- TCP  |  IP: 192.168.0.5  |  Port: 502

## 7. Build, Script, and Repository Efficiency Achievements

## 7.1 Script model split by intent
- setup.sh:
  - restore + Debug build baseline
- run.sh:
  - quick run path with restore safeguards
- verify.sh:
  - Release build + tests

## 7.2 Reliability hardening
- Restore retries added for transient network/feed cancellations.
- NuGet retry env tuning included.

## 7.3 Shared script logic extraction
- common-dotnet.sh created to centralize:
  - DOTNET_CLI_HOME initialization policy
  - restore retry function

## 7.4 Cache and bloat controls
- Slim default cache strategy introduced.
- Options added for explicit cache location control.
- verify.sh can prune release outputs by default after successful verification.
- clean-dev-cache.sh provides one-command generated artifact cleanup.

## 7.5 Runtime output reduction
- App runtime constrained to osx-arm64 for local build outputs.
- Debug and test output size reduced dramatically versus previous multi-platform runtime baggage.

## 7.6 Test dependency slimming
- StoragePathRules moved to Alchemy.Core.
- Tests retargeted from Alchemy to Alchemy.Core where applicable.
- Internals coupling reduced.

## 7.7 Operational size outcome
- Workspace that had regrown into multi-GB due to generated artifacts and caches can now be reduced to small working size through new defaults and clean flows.
- Recent measured post-clean/setup footprints were in the tens of MB range for source + active debug artifacts.

## 8. Documentation Achievements

Expanded and maintained:
- README.md
- FRESH_INSTALL.md
- docs/ALCHEMY_HANDOFF_SUMMARY.md
- docs/ALCHEMY_V1_REFERENCE.md

Added developer utility guidance:
- alias setup
- run helper
- verify and cleanup commands
- cache behavior controls

## 9. Current Working-Tree Delta Snapshot (Important)

As of this handoff, there are staged/unstaged additions and edits beyond the last pushed commit history. These include key efficiency and behavior upgrades:

- Added:
  - scripts/common-dotnet.sh
  - scripts/verify.sh
  - scripts/clean-dev-cache.sh
  - scripts/install-dev-alias.sh
  - run-nxs-dev.command
  - src/Alchemy/AvaloniaToolWindow.cs
  - src/Alchemy/StorageWorkspaceState.cs
  - src/Alchemy.Core/IToolWindow.cs
  - src/Alchemy.Core/StoragePathRules.cs

- Modified:
  - scripts/run.sh
  - scripts/setup.sh
  - scripts/package-macos.sh
  - src/Alchemy/MainWindow.axaml.cs
  - src/Alchemy/AlchemyWindow.axaml.cs
  - src/Alchemy/AlchemyTitleShell.axaml
  - src/Alchemy/AlchemyTitleShell.axaml.cs
  - src/Alchemy/AlchemyTool.cs
  - src/Alchemy/Alchemy.csproj
  - src/Alchemy.Core/ITool.cs
  - src/Alchemy.Core/Alchemy.Core.csproj
  - src/Alchemy.Core/WindowHandle.cs
  - tests/Alchemy.Tests/Alchemy.Tests.csproj
  - tests/Alchemy.Tests/StoragePathRulesTests.cs
  - README.md
  - FRESH_INSTALL.md

- Removed:
  - src/Alchemy/StoragePathRules.cs

## 10. Remaining Gaps and Recommended Next Steps

## 10.1 Architectural debt still concentrated
Large files still contain mixed responsibilities and should be decomposed further:
- AlchemyWindow.axaml.cs
- MainWindow.axaml.cs

Recommended extraction order:
1. Alchemy panel drag/drop coordinator service.
2. Alchemy connection/parser metadata service.
3. MainWindow storage operations service.
4. Shared move/rename/delete service with user-dialog strategy abstraction.

## 10.2 Test coverage expansion
Current automated tests are still narrow relative to UI/interaction complexity.

Priority test targets:
1. Alchemy move/remap path logic for folder moves.
2. External drop target resolution logic.
3. Connection metadata parse cases:
   - TCP with IP/PORT
   - RTU with IP/PORT
   - missing fields
4. History-hover behavior transitions.

## 10.3 Minor cleanup
- Ensure README behavior bullets reflect latest folder move support in Alchemy internal drag description.
- Ensure docs/ALCHEMY_HANDOFF_SUMMARY.md and docs/ALCHEMY_V1_REFERENCE.md mention title-shell connection metadata.

## 11. Validation Checklist for New Maintainers

Run this sequence on a clean working tree:

1. ./scripts/clean-dev-cache.sh
2. ./scripts/setup.sh
3. ./scripts/run.sh
4. ./scripts/verify.sh

Manual UX checks:

1. Nexus:
- drag folder/file between folders
- drag to root target
- side panel root highlighting
- rename and delete behavior

2. Alchemy panel:
- drag file to folder
- drag folder to folder
- drag over back/forward and hover to navigate
- drop into current-level area
- verify path remap for moved folders

3. Alchemy title shell metadata:
- load XML with TYPE/IP/PORT and verify display format
- clear active selection and verify metadata clears

## 12. Key Commands Reference

Daily dev:
- ./scripts/setup.sh
- ./scripts/run.sh

Release confidence:
- ./scripts/verify.sh

Cleanup:
- ./scripts/clean-dev-cache.sh

Optional quality-of-life:
- ./scripts/install-dev-alias.sh
- run-nxs-dev.command

## 13. Commit Milestones (Recent)

- 88b10db 2026-08-01 Harden run/setup scripts and add GitHub deploy helper
- f46e063 2026-08-01 Unify path containment checks and finalize theme fallback token
- d924372 2026-08-01 Optimize folder-state caching and clarify hover timer naming
- 087686c 2026-08-01 Refactor external drops and centralize UI color tokens
- 8af6d50 2026-08-01 Refine drag-drop UX, storage states, and handoff docs
- 67d0b25 2026-07-31 Update documentation
- 437611d 2026-07-31 Improve app interaction consistency
- b714db8 2026-07-30 Alchemy polish and stability set
- 49d8639 2026-07-30 Alchemy/UI backup milestone
- 5b1864b 2026-07-30 Reproducible setup and recovery guide
- 7109fae 2026-07-29 Initial Alchemy foundation

## 14. Handoff Conclusion

The product has moved from early-stage behavior instability and high operational bloat to a controlled, testable, and significantly leaner engineering baseline.

Nexus and Alchemy now share stronger interaction contracts, scripts are safer and more maintainable, Core boundaries are cleaner, and the system is positioned for focused service extraction and deeper automated coverage in the next phase.
