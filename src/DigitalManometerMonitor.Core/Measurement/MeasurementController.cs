using DigitalManometerMonitor.Core.Acquisition;
using DigitalManometerMonitor.Core.Storage;

namespace DigitalManometerMonitor.Core.Measurement;

public sealed class MeasurementController : IDisposable
{
    private const int ReadBlockSamples = 20; // 20 ms at 1000 Hz.
    private const double ReadTimeoutSeconds = 1.0;

    private readonly object _sync = new();
    private readonly Func<MeasurementSettings, IAnalogInputSession> _sessionFactory;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private IAnalogInputSession? _session;
    private CsvMeasurementWriter? _writer;
    private bool _disposed;
    private bool _isRunning;
    private string? _outputPath;

    public MeasurementController(Func<MeasurementSettings, IAnalogInputSession>? sessionFactory = null)
    {
        _sessionFactory = sessionFactory ?? (settings => new NIDaqVoltageSession(settings));
    }

    public event EventHandler<MeasurementUpdate>? Updated;
    public event EventHandler<MeasurementCompletedEventArgs>? Completed;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _isRunning;
        }
    }

    public string? CurrentOutputPath
    {
        get
        {
            lock (_sync)
                return _outputPath;
        }
    }

    public Task StartAsync(MeasurementSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfDisposed();
        settings.Validate();

        lock (_sync)
        {
            if (_isRunning)
                throw new InvalidOperationException("A measurement is already running.");
        }

        string outputPath = settings.GetOutputPath();
        if (File.Exists(outputPath))
            throw new IOException($"The output file already exists:\n{outputPath}");

        var cts = new CancellationTokenSource();
        CsvMeasurementWriter? writer = null;
        IAnalogInputSession? session = null;
        bool outputFileCreatedByThisRun = false;

        try
        {
            // FileMode.CreateNew inside CsvMeasurementWriter is the atomic, race-safe collision check.
            writer = new CsvMeasurementWriter(outputPath, settings, DateTimeOffset.Now);
            outputFileCreatedByThisRun = true;
            session = _sessionFactory(settings);
            session.Start();
        }
        catch
        {
            session?.Dispose();
            writer?.Dispose();

            // Remove only a setup artifact that this run actually created. Never delete a pre-existing file.
            if (outputFileCreatedByThisRun)
            {
                try
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch
                {
                    // Do not hide the original setup error.
                }
            }

            cts.Dispose();
            throw;
        }

        lock (_sync)
        {
            _cts = cts;
            _writer = writer;
            _session = session;
            _outputPath = outputPath;
            _isRunning = true;
            _runTask = Task.Run(() => RunLoop(settings, cts.Token));
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? task;
        lock (_sync)
        {
            if (!_isRunning)
                return;

            _cts?.Cancel();
            task = _runTask;
        }

        if (task is not null)
            await task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Dispose must still release resources.
        }

        lock (_sync)
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void RunLoop(MeasurementSettings settings, CancellationToken cancellationToken)
    {
        long sampleCount = 0;
        long? targetSamples = settings.GetTargetSampleCount();
        var voltageBuffer = new double[ReadBlockSamples];
        var pressureBuffer = new double[ReadBlockSamples];
        MeasurementStopReason reason = MeasurementStopReason.Manual;
        Exception? fault = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int requested = ReadBlockSamples;
                if (targetSamples.HasValue)
                {
                    long remaining = targetSamples.Value - sampleCount;
                    if (remaining <= 0)
                    {
                        reason = MeasurementStopReason.TimerCompleted;
                        break;
                    }

                    requested = (int)Math.Min(requested, remaining);
                }

                IAnalogInputSession session = GetSessionOrThrow();
                int read = session.Read(voltageBuffer, requested, ReadTimeoutSeconds);
                if (read <= 0)
                    continue;

                for (int i = 0; i < read; i++)
                    pressureBuffer[i] = settings.Scale.ToPressurePa(voltageBuffer[i]);

                CsvMeasurementWriter writer = GetWriterOrThrow();
                writer.WritePressureSamples(pressureBuffer, read, sampleCount);

                sampleCount += read;
                double elapsed = sampleCount / MeasurementSettings.SampleRateHz;
                Updated?.Invoke(this, new MeasurementUpdate(sampleCount, elapsed, pressureBuffer[read - 1]));

                if (targetSamples.HasValue && sampleCount >= targetSamples.Value)
                {
                    reason = MeasurementStopReason.TimerCompleted;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            reason = MeasurementStopReason.Faulted;
            fault = ex;
        }
        finally
        {
            Exception? cleanupFault = CleanupRun();
            if (fault is null && cleanupFault is not null)
            {
                reason = MeasurementStopReason.Faulted;
                fault = cleanupFault;
            }

            string outputPath;
            lock (_sync)
            {
                outputPath = _outputPath ?? string.Empty;
                _isRunning = false;
            }

            Completed?.Invoke(this, new MeasurementCompletedEventArgs(
                reason,
                outputPath,
                sampleCount,
                sampleCount / MeasurementSettings.SampleRateHz,
                fault));
        }
    }

    private IAnalogInputSession GetSessionOrThrow()
    {
        lock (_sync)
            return _session ?? throw new InvalidOperationException("DAQ session is not available.");
    }

    private CsvMeasurementWriter GetWriterOrThrow()
    {
        lock (_sync)
            return _writer ?? throw new InvalidOperationException("CSV writer is not available.");
    }

    private Exception? CleanupRun()
    {
        IAnalogInputSession? session;
        CsvMeasurementWriter? writer;
        CancellationTokenSource? cts;

        lock (_sync)
        {
            session = _session;
            writer = _writer;
            cts = _cts;
            _session = null;
            _writer = null;
            _cts = null;
            _runTask = null;
        }

        Exception? firstError = null;

        if (session is not null)
        {
            try { session.Stop(); }
            catch (Exception ex) { firstError ??= ex; }

            try { session.Dispose(); }
            catch (Exception ex) { firstError ??= ex; }
        }

        if (writer is not null)
        {
            try { writer.Dispose(); }
            catch (Exception ex) { firstError ??= ex; }
        }

        cts?.Dispose();
        return firstError;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
