from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
main = (ROOT / 'src/DigitalManometerMonitor/MainForm.cs').read_text(encoding='utf-8')
plot = (ROOT / 'src/DigitalManometerMonitor/PressurePlotControl.cs').read_text(encoding='utf-8')
preview = (ROOT / 'src/DigitalManometerMonitor.Core/Measurement/PreviewMonitorController.cs').read_text(encoding='utf-8')
ni = (ROOT / 'src/DigitalManometerMonitor.Core/Acquisition/NIDaqVoltageSession.cs').read_text(encoding='utf-8')

checks = [
    ('SplitContainer removed', 'SplitContainer' not in main),
    ('main layout uses 38 percent left column', 'new ColumnStyle(SizeType.Percent, 38F)' in main),
    ('main layout uses 62 percent monitor column', 'new ColumnStyle(SizeType.Percent, 62F)' in main),
    ('header row autosizes', 'root.RowStyles.Add(new RowStyle(SizeType.AutoSize));' in main),
    ('base UI font is laptop-readable 10.5F', 'Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);' in main),
    ('row labels fill and center in their cells', 'MinimumSize = new Size(0, 28)' in main and 'TextAlign = ContentAlignment.MiddleLeft' in main),
    ('current pressure area autosizes for DPI', 'valuePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));' in main),
    ('DAQ refresh is a full-width row', 'AddSpanRow(table, _refreshChannelsButton);' in main),
    ('measurement start has an explicit transition guard', '_measurementTransitionActive' in main and 'SetMeasurementStartingUiState();' in main),
    ('start button state depends on active preview', '_previewController.IsRunning' in main and 'UpdateStartButtonState();' in main),
    ('measurement locks UI before stopping preview', (lambda a,b: a >= 0 and b >= 0 and a < b)(main.find('SetMeasurementStartingUiState();', main.find('private async Task StartMeasurementAsync')), main.find('await _previewController.StopAsync();', main.find('private async Task StartMeasurementAsync')))),
    ('startup invokes DAQ refresh', 'await RefreshChannelsAsync();' in main),
    ('preview restarts from selected channel', 'await StartPreviewForSelectedChannelAsync(updateMainStatus: true);' in main),
    ('axis title font is 12F regular', 'new Font("Segoe UI", 12F, FontStyle.Regular)' in plot),
    ('plot margins measure rendered text', 'g.MeasureString(yLabels[i], tickFont)' in plot and 'float leftMargin =' in plot),
    ('preview samples in hardware-timed blocks', 'ReadBlockSamples = 50' in preview),
    ('USB-6000 uses RSE', 'NIDaqmxNative.ValRse' in ni),
    ('DAQ hardware range is minus ten to plus ten volts', 'HardwareMinimumVoltageV = -10.0' in ni and 'HardwareMaximumVoltageV = 10.0' in ni),
]

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL'), name)

if failed:
    print(f'\n{len(failed)} static source check(s) failed.')
    raise SystemExit(1)
print('\nAll static source checks passed.')
