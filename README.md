# Digital Manometer Pressure Monitor

This is a Windows desktop application for reading a pressure transducer or digital manometer analog voltage output through a National Instruments DAQ device. It displays pressure continuously, plots a measurement run, and saves pressure data to CSV.

The goal of this README is that a person starting from a new Windows PC, without Visual Studio or device drivers installed yet, can understand what to prepare and how to run the program.

## What This Program Does

- Detects National Instruments analog input channels through NI-DAQmx
- Continuously displays the current pressure before, during, and after logging
- Acquires data with a 1000 Hz hardware-timed NI-DAQmx task
- Converts voltage to pressure with an editable two-point linear calibration
- Saves CSV files with manual stop or timer stop
- Restarts live monitoring automatically after a logged measurement ends
- Avoids overwriting existing CSV files

CSV output includes metadata and pressure samples:

```text
# Digital Manometer Pressure Measurement
# MeasurementStartLocal,2026-10-05T14:13:00.0000000+09:00
# AIChannel,Dev1/ai0
# TerminalConfiguration,RSE
# SampleRate_Hz,1000
ElapsedTime_s,Pressure_Pa
0.000000,123.456000
0.001000,124.001000
```

## Hardware Compatibility

The developer tested this application with the NI USB-6000 style workflow: an NI DAQ device visible in NI MAX, with a voltage-output pressure device connected to an analog input channel.

The code is not limited to one exact NI USB model. It should work with any National Instruments DAQ device that:

- is supported by NI-DAQmx on Windows;
- appears in NI MAX;
- exposes at least one analog input channel;
- supports RSE analog voltage input;
- supports a -10 V to +10 V input range;
- can sustain 1000 samples/s on the selected channel.

The pressure device also does not have to be one exact model. Any pressure transducer, digital manometer, or signal conditioner can be used if it outputs a voltage that can be mapped linearly to pressure. The default calibration assumes 0 V = 0 Pa and 10 V = 100000 Pa, but those values are editable in the GUI.

Prepare:

- 64-bit Windows 10 or Windows 11 PC
- National Instruments DAQ device supported by NI-DAQmx
- Pressure device with analog voltage output
- Signal wire from pressure output to a DAQ analog input
- Return/common wire from pressure output to DAQ analog ground
- Folder for CSV data

## Install on a Fresh PC

1. Run Windows Update.
2. Install Visual Studio 2022.
3. In Visual Studio Installer, select the `.NET desktop development` workload.
4. Install the .NET 8 SDK if it was not installed with Visual Studio.
5. Install NI-DAQmx.
6. Open NI MAX and confirm that the DAQ device is visible.

NI MAX is strongly recommended because it shows the device name and available analog input channels and helps verify wiring before running this program.

## Wiring

The application configures the analog input as RSE.

```text
Pressure-device analog output signal  -> DAQ AIx terminal, for example AI0
Pressure-device analog output common  -> DAQ AI GND
```

The DAQ voltage input range is fixed in software at -10 V to +10 V. The pressure conversion is configured separately in the GUI under `Linear Pressure Scale`.

Default conversion:

- Voltage Low = 0 V
- Voltage High = 10 V
- Pressure at Low = 0 Pa
- Pressure at High = 100000 Pa

Change these four values to match the actual pressure device.

## Build and Run

1. Download the repository from GitHub with `Code` -> `Download ZIP`, or clone it with Git.
2. If you downloaded a ZIP file, extract it to a normal working folder.
3. Open `DigitalManometerMonitor.sln` in Visual Studio 2022.
4. Select `Release` and `x64`.
5. Confirm that `DigitalManometerMonitor` is the startup project.
6. Run `Build` -> `Build Solution`.
7. Start the program with `Start` or `F5`.

You can also build from PowerShell:

```powershell
.\build-release.ps1
```

The built application is normally created at:

```text
src\DigitalManometerMonitor\bin\x64\Release\net8.0-windows\DigitalManometerMonitor.exe
```

## Basic Operation

1. Connect the NI DAQ device to the PC.
2. Confirm in NI MAX that the device is visible.
3. Wire the pressure device voltage output to the selected DAQ analog input and analog ground.
4. Start the application.
5. The application scans available analog input channels at startup and starts live monitoring when possible.
6. Click `Detect / Refresh DAQ` if the device was connected after startup.
7. Select the desired AI channel in `DAQ Input`.
8. Set `Linear Pressure Scale` for the pressure device.
9. Choose the CSV folder and file name.
10. Select `Manual` or `Timer (s)`.
11. Click `Start Measurement`.
12. In manual mode, click `Stop Measurement` when finished.

Only one NI-DAQmx task owns the selected input channel at a time. The program stops preview monitoring before logging and starts preview monitoring again after logging ends.

## Before Using Experimental Data

Check the following on the actual setup:

- NI MAX shows the DAQ device.
- The selected analog input channel and ground wiring are correct.
- A known 0 V, 5 V, or 10 V signal produces the expected pressure after calibration.
- 1000 samples/s is stable on the selected hardware.
- CSV timestamps advance by 0.001 s.
- The pressure calibration values in the GUI match the actual pressure device.

## Tests

Hardware-independent self-tests cover pressure scaling, DAQ range constants, timer sample count, CSV output, overwrite protection, and live-preview behavior.

```powershell
.\run-self-tests.ps1
```

or:

```powershell
dotnet run --project .\tests\DigitalManometerMonitor.SelfTests\DigitalManometerMonitor.SelfTests.csproj
```

If Python is installed, a static source check is also available:

```powershell
python .\tests\static_source_checks.py
```

## Troubleshooting

- No DAQ channel appears: check NI-DAQmx, USB connection, and NI MAX.
- Pressure stays near zero: check signal wiring, analog ground, and pressure-device output settings.
- Pressure is scaled incorrectly: check the four `Linear Pressure Scale` values.
- CSV cannot be created: check folder write permission and whether a file with the same name already exists.
- Measurement fails to start: close NI MAX test panels or other software that may already be using the same channel.
