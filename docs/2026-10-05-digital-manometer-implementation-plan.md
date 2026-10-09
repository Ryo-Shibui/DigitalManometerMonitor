# Digital Manometer Monitor Revised Implementation Plan

> **Execution:** Native inline execution in this session; no user confirmation pauses.

**Goal:** Rebuild the WinForms UI for comfortable default laptop use, add automatic DAQ detection and continuous preview monitoring, and preserve 1000 Hz logged acquisition with safe CSV output.

**Architecture:** Remove the SplitContainer/fixed-width UI and use a percentage-based TableLayoutPanel. Separate preview monitoring from logged measurement so only one NI-DAQmx task owns the selected AI channel at a time. Keep DAQ timing hardware-clocked at 1000 Hz while throttling UI rendering independently.

**Tech Stack:** C# 12, .NET 8, Windows Forms, NI-DAQmx native C API (`nicaiu.dll`), no third-party NuGet packages.

**Spec:** `docs/2026-10-05-digital-manometer-design.md`

## Global Constraints

- NI USB-6000, RSE input.
- DAQ hardware input range fixed at -10 V to +10 V.
- Sample rate 1000 Hz.
- Default pressure scale 0 V -> 0 Pa, 10 V -> 100000 Pa.
- Full-rate CSV logging; UI updates may be decimated.
- Existing files must never be overwritten.
- Graph axis titles: Segoe UI 12F, Regular, not Bold.
- Typical 1366x768-class laptop should require no manual width adjustment.

## Tasks

### Task 1: Decouple DAQ hardware range from pressure calibration
- Update `NIDaqVoltageSession` to use fixed -10 V/+10 V hardware limits.
- Add CSV metadata for DAQ hardware range.
- Add/extend self-tests for metadata.

### Task 2: Add preview monitor controller
- Add a controller that owns a hardware-timed 1000 Hz AI session while no logged measurement is active.
- Publish current pressure at approximately 20 Hz.
- Support safe stop/restart for refresh, channel changes, measurement start, and measurement completion.
- Add injectable-session self-tests.

### Task 3: Rebuild MainForm responsive layout
- Replace SplitContainer and fixed 360 px GroupBoxes with percentage-based TableLayoutPanel layout.
- Use DPI scaling, screen-derived initial bounds, aligned shared two-column rows, and compact auto-sized sections.
- Add `Detect / Refresh DAQ`, DAQ status, startup auto-detection, preview start, and automatic preview restart.

### Task 4: Rebuild plot margin calculation
- Measure tick-label and axis-title text to derive margins dynamically.
- Keep axis titles Segoe UI 12F Regular.
- Ensure tick labels and titles cannot overlap under normal sizing.

### Task 5: Documentation and package verification
- Update README for USB-6000 RSE wiring, auto-preview behavior, and UI behavior.
- Run all available local verification; if .NET SDK is unavailable, explicitly report that limitation and run structural/static checks.
- Recreate `DigitalManometerMonitor_Complete.zip`.

## Final hardening applied

- Main layout finalized at 38% controls / 62% monitor area.
- Header and status rows use AutoSize to avoid DPI clipping.
- Base UI font finalized at Segoe UI 10.5F Regular.
- Row labels use fill + vertical centering with a common minimum row height.
- `Detect / Refresh DAQ` occupies a full-width row below the AI selector to prevent row-height and width distortion.
- Measurement start has an explicit transition state: controls lock before preview shutdown, preventing double-start races.
- `Start Measurement` is enabled only while a valid preview task is actually running and the pressure scale is valid.
- Static source checks were added under `tests/static_source_checks.py` for layout/DAQ invariants.
