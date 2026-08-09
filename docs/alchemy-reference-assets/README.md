# Alchemy Reference Assets (Imported 2026-08-04)

This folder stores external source artifacts used to validate and maintain Alchemy dictionary and export behavior.

## Files

1. `UTICOR_DICTIONARY_2026-08-04.rtf`
- Source: desktop-provided UTICOR reference document.
- Purpose: authoritative human-readable dictionary for UTICOR datatype, encode, verify, function code, valid combinations, exception combinations, and preload rules.
- Operational use in Alchemy:
  - Used as the reference baseline when updating `src/Alchemy/Data/Uticor Dictionary.txt`.
  - Used to verify whether an observed Datatype+Encode pair should map to a known PLC DataType, be treated as an exception repair, or be treated as unknown.
  - Used to reason about expected output datalength in exported XML and in Alchemy mismatch-risk UI.

2. `PLCTaglistExcelMacro_2026-08-04.bas`
- Source: desktop-provided VBA module for XML/JSON export.
- Purpose: implementation contract for how Excel output is produced from PLC tag rows.
- Operational use in Alchemy:
  - Defines XML export behavior that Alchemy mirrors for risk signaling:
    - `BOOL (Bit of INT)` -> datalength string like `1[bit]` using Modbus bit index.
    - Data types containing `DINT` or `REAL` -> datalength `2`.
    - Other data types -> datalength `1`.
  - Defines function-code mapping used when validating read semantics:
    - `BOOL` -> `01`
    - `BOOL (Bit of INT)` -> `03`
    - all other rows -> `03`
  - Defines preload generation behavior (`Preload_Words_*`, `Preload_Bits_*`) from clustered/chunked address windows.

3. `PLC_Tag_List_2026-08-04.xlsm`
- Source: desktop-provided workbook used with the VBA macro.
- Purpose: canonical runtime workbook for generating sample/real exports.
- Operational use in Alchemy:
  - Used to run/export XML and compare generated rows against Alchemy interpretation.
  - Used to reproduce edge cases in datalength, function code, preload assignment, and datatype mapping.

## Recommended Validation Workflow

1. Export XML from the `.xlsm` workbook using the `.bas` macro.
2. Open the XML in Alchemy.
3. Verify:
- Datatype/encode display and repair behavior.
- Unknown/repaired/scaling/conflict diagnostics.
- Datalength mismatch line and red risk emphasis only when source XML length differs from inferred Excel output length.
- Preloads excluded from mismatch risk.

## Notes

- These files are stored as immutable reference snapshots by date for handoff continuity.
- If new source versions are provided, keep old snapshots and add new timestamped copies.
