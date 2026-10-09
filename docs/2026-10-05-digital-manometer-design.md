# Digital Manometer Pressure Monitor — Revised Design

## Goal

Build a Windows desktop application for Visual Studio 2022 that uses an NI USB-6000 to continuously monitor a digital manometer analog output, display the current pressure in real time, optionally log a measurement to CSV at 1000 Hz, and remain comfortable to use on a typical laptop without any manual panel-width adjustment.

## Hardware assumptions

- NI DAQ model: USB-6000.
- Analog input terminal configuration: RSE (Referenced Single-Ended).
- User selects the AI channel from detected NI-DAQmx physical channels.
- DAQ acquisition rate: 1000 samples/s.
- DAQ hardware voltage range: -10 V to +10 V, independent of the pressure calibration range.
- Default pressure calibration: 0 V -> 0 Pa and 10 V -> 100000 Pa.
- Pressure calibration remains user-editable as a two-point linear scale.

## Application architecture

The application is divided into five responsibilities.

1. **NI-DAQmx native interop**
   - P/Invoke wrapper around `nicaiu.dll`.
   - Physical AI-channel discovery.
   - Single-channel, hardware-timed RSE voltage acquisition.

2. **Preview monitor**
   - Runs whenever a valid AI channel is available and no logged measurement is active.
   - Uses the same 1000 Hz hardware-timed acquisition approach as logged measurement.
   - Updates only `Current Pressure`; it does not write CSV and does not advance measurement elapsed time.
   - Restarts automatically after a logged measurement finishes.
   - Restarts automatically after the user selects another AI channel.

3. **Measurement controller**
   - Stops preview before starting a logged measurement so two NI tasks do not compete for the same AI resource.
   - Acquires at 1000 Hz on a background task.
   - Converts every sample to pressure.
   - Streams every sample to CSV.
   - Publishes decimated UI updates without changing stored data.
   - Supports manual stop and timer stop.
   - Restarts preview after normal stop, timed stop, or recoverable measurement termination.

4. **CSV storage**
   - Uses `FileMode.CreateNew` so an existing path can never be overwritten.
   - Measurement refuses to start if the requested path already exists.
   - Writes measurement metadata and every 1 ms sample.
   - Uses invariant numeric formatting.

5. **WinForms UI**
   - Responsive layout based primarily on `TableLayoutPanel` rather than fixed-width nested controls.
   - No user-adjustable `SplitContainer` is required for normal use.
   - Controls are aligned consistently across rows.
   - Layout scales acceptably with Windows DPI settings and a typical 1366x768-class laptop display.

## UI layout

### Main window

- English-only UI.
- Main content uses 38% width for controls and 62% width for monitoring/graphing, chosen to keep English labels and inputs comfortable on a 1366x768-class laptop.
- Window initial bounds are derived from `Screen.WorkingArea` rather than a single fixed pixel size.
- Target initial size is about 90% of the available working area, subject to sensible minimum/maximum bounds.
- Minimum size remains large enough to avoid clipped labels.
- The form uses AutoScaleMode.Dpi. The base UI font is Segoe UI 10.5F Regular; larger status/value fonts are used only where appropriate.

### Left control area

Sections:

1. DAQ Input
2. Linear Pressure Scale
3. CSV Output
4. Measurement Stop Mode
5. Measurement

All sections fill the available left-column width. Fixed group widths such as 360 px are removed. Labels and input controls use a shared two-column layout so rows align vertically and horizontally. The DAQ refresh button occupies its own full-width row so it cannot squeeze the AI channel selector or create a one-off row height.

### DAQ Input section

Contains:

- `AI Channel` combo box.
- `Detect / Refresh DAQ` button.
- `Terminal Mode: RSE (fixed)` display.
- `DAQ Status` display.

Behavior:

- Application performs DAQ discovery at startup.
- If one or more AI channels are found, the first valid channel is selected automatically.
- Preview monitoring starts automatically after successful detection.
- `Detect / Refresh DAQ` stops any preview safely, rescans NI hardware, repopulates the channel list, selects an available channel, and restarts preview.
- Changing `AI Channel` restarts preview on the newly selected channel.
- During an active logged measurement, channel selection and refresh are disabled.

### Current Pressure

- Visible before, during, and after logged measurements whenever DAQ acquisition is available.
- Display unit: Pa.
- Uses a large, high-contrast numeric value.
- UI refresh is decoupled from sample acquisition; approximately 20 visual updates/s is sufficient while DAQ and CSV remain at 1000 samples/s.

### Graph

- Graph begins a new trace when `Start Measurement` is pressed.
- X axis: `Measurement Time (s)`.
- Y axis: `Pressure (Pa)`.
- Stored/DAQ data remain 1000 Hz; graph rendering may decimate display points.
- Plot margins are calculated from measured text extents instead of hard-coded values that assume a specific font size.
- Tick labels and axis titles must not overlap.
- Axis title font: **Segoe UI, 12F, Regular (not Bold)**.
- Tick label font is sized for comfortable laptop viewing and remains Regular.
- Graph control invalidation rate is limited so rendering cannot starve acquisition.

### Elapsed time

- Lower-right status area.
- Display format: `Elapsed: 0 s`, `Elapsed: 1 s`, ...
- Always seconds only; no conversion to minutes or hours.
- Updated once per second while a logged measurement is active.
- Preview monitoring does not advance elapsed time.

## Acquisition and UI timing

### DAQ timing

- NI-DAQmx sample clock: exactly 1000 Hz nominal hardware-timed continuous sampling.
- Acquisition reads blocks on a worker/background task.
- UI timers are never used to generate the sample timing.

### Display timing

- Current pressure: approximately 20 updates/s.
- Graph: approximately 10-20 updates/s.
- Elapsed display: 1 update/s.
- All CSV samples remain full rate.

## Scaling

Pressure is calculated from two user-specified calibration points:

- `(V_low, P_low)`
- `(V_high, P_high)`

using linear interpolation/extrapolation.

The DAQ hardware range remains -10 V to +10 V regardless of pressure-scale values.

Default calibration:

- 0 V -> 0 Pa
- 10 V -> 100000 Pa

No pressure clamping is applied unless NI hardware itself clips outside its input range.

## Measurement behavior

### Start Measurement

Before start:

- Validate selected AI channel.
- Validate calibration values.
- Validate output directory and filename.
- Validate timer duration when timer mode is selected.
- Reject start if output path already exists.

Then:

1. Stop preview.
2. Atomically create the CSV using `CreateNew`.
3. Start a 1000 Hz logged acquisition task.
4. Clear graph and elapsed display.
5. Stream samples to CSV and update Current Pressure/graph.

If logged acquisition cannot start, close any newly created empty output file when safe, report the error, and restore preview monitoring.

### Manual stop

`Stop Measurement` stops acquisition, flushes/closes CSV, keeps the completed graph visible, resets elapsed state appropriately, and restarts preview.

### Timer stop

- User enters duration in seconds.
- The target stop point is calculated from samples, not UI timer ticks.
- On reaching the requested duration, acquisition stops automatically, CSV is finalized, and preview restarts.

## CSV format

CSV contains metadata followed by sample rows.

Metadata includes at least:

- Measurement start date/time from the PC clock.
- Selected AI channel.
- Terminal configuration: RSE.
- Sample rate: 1000 Hz.
- DAQ hardware input range: -10 V to +10 V.
- User pressure calibration values.

Data columns:

- Measurement Time (s)
- Pressure (Pa)

Elapsed sample time is calculated from sample index divided by 1000 so nominal timestamps advance exactly by 0.001 s.

## File safety

- Existing CSV paths are rejected before measurement starts.
- `FileMode.CreateNew` is the final race-safe enforcement.
- Existing files are never overwritten.
- Data are streamed and periodically flushed so already-written samples are retained if acquisition later faults.

## Error handling

Errors shown to the user include:

- NI-DAQmx runtime missing.
- No NI devices or AI channels detected.
- Device unplugged or reset.
- Requested AI channel unavailable.
- DAQ task creation/read failure.
- Invalid calibration.
- Invalid timer duration.
- Output directory unavailable.
- Output filename collision.
- CSV write failure.

The application must not crash because preview acquisition loses the device. Preview faults transition the UI to a disconnected/error state and allow `Detect / Refresh DAQ` to recover.

## Control enable/disable rules

### Preview-only state

Enabled:

- DAQ refresh.
- AI channel selection.
- Pressure-scale controls.
- CSV path controls.
- stop-mode controls.
- Start Measurement.

Disabled:

- Stop Measurement.

### Logged measurement state

Disabled:

- DAQ refresh.
- AI channel selection.
- Pressure-scale controls.
- CSV path controls.
- stop-mode controls.
- Start Measurement.

Enabled:

- Stop Measurement.

## Shutdown behavior

- If no measurement is active, stop preview and close normally.
- If a logged measurement is active, ask for confirmation before stopping and closing the CSV.

## Verification strategy

Hardware-independent tests cover:

- Pressure scaling.
- Measurement-settings validation.
- CSV no-overwrite behavior and metadata.
- Timer sample-count calculation.
- Preview/measurement state-transition logic where isolatable.

Windows build verification must be performed with .NET 8 / Visual Studio-compatible tooling.

Hardware acceptance testing on USB-6000 must verify:

1. Startup discovery finds the expected AI channel.
2. Preview pressure updates without pressing Start Measurement.
3. Refresh recovers after unplug/replug where NI-DAQmx makes the device available again.
4. Start stops preview and begins logging without resource-conflict errors.
5. CSV records 1000 rows/s nominally.
6. Manual stop closes the file and preview resumes.
7. Timer stop closes the file automatically at the requested sample count and preview resumes.
8. Existing output path prevents start and leaves the existing file unchanged.
9. Layout is usable without resizing on a typical 1366x768 laptop at common Windows scaling settings.
10. Graph tick labels and 12F Regular axis titles do not overlap.
