using DigitalManometerMonitor.Core.Measurement;

namespace DigitalManometerMonitor.Core.Acquisition;

public sealed class NIDaqVoltageSession : IAnalogInputSession
{
    private const ulong ContinuousBufferSamples = 100_000;

    public const double HardwareMinimumVoltageV = -10.0;
    public const double HardwareMaximumVoltageV = 10.0;

    private readonly string _physicalChannel;
    private IntPtr _taskHandle;
    private bool _started;
    private bool _disposed;

    public NIDaqVoltageSession(string physicalChannel)
    {
        if (string.IsNullOrWhiteSpace(physicalChannel))
            throw new ArgumentException("Select an analog input channel.", nameof(physicalChannel));

        _physicalChannel = physicalChannel.Trim();
    }

    public NIDaqVoltageSession(MeasurementSettings settings)
        : this((settings ?? throw new ArgumentNullException(nameof(settings))).PhysicalChannel)
    {
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_started)
            throw new InvalidOperationException("The NI-DAQmx session is already running.");

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NI-DAQmx acquisition is supported only on Windows.");

        try
        {
            NIDaqmxNative.Check(NIDaqmxNative.DAQmxCreateTask("DigitalManometerPressureTask", out _taskHandle));

            NIDaqmxNative.Check(NIDaqmxNative.DAQmxCreateAIVoltageChan(
                _taskHandle,
                _physicalChannel,
                "PressureVoltage",
                NIDaqmxNative.ValRse,
                HardwareMinimumVoltageV,
                HardwareMaximumVoltageV,
                NIDaqmxNative.ValVolts,
                null));

            NIDaqmxNative.Check(NIDaqmxNative.DAQmxCfgSampClkTiming(
                _taskHandle,
                string.Empty,
                MeasurementSettings.SampleRateHz,
                NIDaqmxNative.ValRising,
                NIDaqmxNative.ValContinuousSamples,
                ContinuousBufferSamples));

            NIDaqmxNative.Check(NIDaqmxNative.DAQmxStartTask(_taskHandle));
            _started = true;
        }
        catch
        {
            SafeClearTask();
            throw;
        }
    }

    public int Read(double[] destination, int samplesRequested, double timeoutSeconds)
    {
        ThrowIfDisposed();
        if (!_started)
            throw new InvalidOperationException("The NI-DAQmx session has not been started.");
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        if (samplesRequested <= 0 || samplesRequested > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(samplesRequested));

        int samplesRead;
        NIDaqmxNative.Check(NIDaqmxNative.DAQmxReadAnalogF64(
            _taskHandle,
            samplesRequested,
            timeoutSeconds,
            NIDaqmxNative.ValGroupByChannel,
            destination,
            (uint)destination.Length,
            out samplesRead,
            IntPtr.Zero));

        return samplesRead;
    }

    public void Stop()
    {
        if (_disposed || !_started || _taskHandle == IntPtr.Zero)
            return;

        try
        {
            int status = NIDaqmxNative.DAQmxStopTask(_taskHandle);
            if (status < 0)
                NIDaqmxNative.Check(status);
        }
        finally
        {
            _started = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            if (_started)
            {
                try { Stop(); }
                catch { /* ClearTask below is the final cleanup path. */ }
            }
        }
        finally
        {
            SafeClearTask();
            _disposed = true;
        }
    }

    private void SafeClearTask()
    {
        if (_taskHandle == IntPtr.Zero)
            return;

        try
        {
            _ = NIDaqmxNative.DAQmxClearTask(_taskHandle);
        }
        finally
        {
            _taskHandle = IntPtr.Zero;
            _started = false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
