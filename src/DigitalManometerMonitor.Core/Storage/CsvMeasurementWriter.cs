using System.Globalization;
using System.Text;
using DigitalManometerMonitor.Core.Measurement;
using DigitalManometerMonitor.Core.Acquisition;

namespace DigitalManometerMonitor.Core.Storage;

public sealed class CsvMeasurementWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly double _sampleRateHz;
    private bool _disposed;
    private long _samplesSinceFlush;

    public CsvMeasurementWriter(string outputPath, MeasurementSettings settings, DateTimeOffset startLocal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(settings);

        OutputPath = outputPath;
        _sampleRateHz = MeasurementSettings.SampleRateHz;

        var stream = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);

        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 64 * 1024);
        WriteHeader(settings, startLocal);
    }

    public string OutputPath { get; }

    public void WritePressureSamples(double[] pressuresPa, int count, long startingSampleIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pressuresPa is null)
            throw new ArgumentNullException(nameof(pressuresPa));
        if (count < 0 || count > pressuresPa.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (startingSampleIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startingSampleIndex));

        var block = new StringBuilder(Math.Max(256, count * 32));
        for (int i = 0; i < count; i++)
        {
            double elapsedSeconds = (startingSampleIndex + i) / _sampleRateHz;
            block.Append(elapsedSeconds.ToString("F6", CultureInfo.InvariantCulture));
            block.Append(',');
            block.Append(pressuresPa[i].ToString("F6", CultureInfo.InvariantCulture));
            block.AppendLine();
        }

        _writer.Write(block.ToString());
        _samplesSinceFlush += count;

        if (_samplesSinceFlush >= (long)_sampleRateHz)
        {
            _writer.Flush();
            _samplesSinceFlush = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _writer.Flush();
        _writer.Dispose();
        _disposed = true;
    }

    private void WriteHeader(MeasurementSettings settings, DateTimeOffset startLocal)
    {
        _writer.WriteLine("# Digital Manometer Pressure Measurement");
        _writer.WriteLine($"# MeasurementStartLocal,{Escape(startLocal.ToString("O", CultureInfo.InvariantCulture))}");
        _writer.WriteLine($"# AIChannel,{Escape(settings.PhysicalChannel)}");
        _writer.WriteLine("# TerminalConfiguration,RSE");
        _writer.WriteLine($"# SampleRate_Hz,{MeasurementSettings.SampleRateHz.ToString("F0", CultureInfo.InvariantCulture)}");
        _writer.WriteLine($"# DAQHardwareInputMinimum_V,{NIDaqVoltageSession.HardwareMinimumVoltageV.ToString("G17", CultureInfo.InvariantCulture)}");
        _writer.WriteLine($"# DAQHardwareInputMaximum_V,{NIDaqVoltageSession.HardwareMaximumVoltageV.ToString("G17", CultureInfo.InvariantCulture)}");
        _writer.WriteLine($"# VoltageLow_V,{settings.Scale.VoltageLowV.ToString("G17", CultureInfo.InvariantCulture)}");
        _writer.WriteLine($"# VoltageHigh_V,{settings.Scale.VoltageHighV.ToString("G17", CultureInfo.InvariantCulture)}");
        _writer.WriteLine($"# PressureAtVoltageLow_Pa,{settings.Scale.PressureAtLowPa.ToString("G17", CultureInfo.InvariantCulture)}");
        _writer.WriteLine($"# PressureAtVoltageHigh_Pa,{settings.Scale.PressureAtHighPa.ToString("G17", CultureInfo.InvariantCulture)}");
        _writer.WriteLine("ElapsedTime_s,Pressure_Pa");
        _writer.Flush();
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
