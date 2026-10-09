using DigitalManometerMonitor.Core.Acquisition;

namespace DigitalManometerMonitor.Core.Measurement;

public sealed class PreviewMonitorController : IDisposable
{
    // 50 samples at 1000 Hz -> one UI-worthy update approximately every 50 ms (20 Hz).
    private const int ReadBlockSamples = 50;
    private const double ReadTimeoutSeconds = 1.0;

    private readonly object _sync = new();
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private readonly Func<string, IAnalogInputSession> _sessionFactory;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private IAnalogInputSession? _session;
    private PressureScale _scale = new(0, 10, 0, 100_000);
    private bool _isRunning;
    private bool _disposed;

    public PreviewMonitorController(Func<string, IAnalogInputSession>? sessionFactory = null)
    {
        _sessionFactory = sessionFactory ?? (channel => new NIDaqVoltageSession(channel));
    }

    public event EventHandler<PreviewPressureUpdate>? Updated;
    public event EventHandler<PreviewFaultedEventArgs>? Faulted;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _isRunning;
        }
    }

    public void UpdateScale(PressureScale scale)
    {
        ThrowIfDisposed();
        scale.Validate();
        lock (_sync)
            _scale = scale;
    }

    public async Task StartAsync(string physicalChannel, PressureScale scale)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(physicalChannel))
            throw new ArgumentException("Select an analog input channel.", nameof(physicalChannel));
        scale.Validate();

        await _transitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);

            var cts = new CancellationTokenSource();
            IAnalogInputSession? session = null;
            try
            {
                session = _sessionFactory(physicalChannel.Trim());
                session.Start();
            }
            catch
            {
                session?.Dispose();
                cts.Dispose();
                throw;
            }

            lock (_sync)
            {
                _scale = scale;
                _cts = cts;
                _session = session;
                _isRunning = true;
                _runTask = Task.Run(() => RunLoop(cts.Token));
            }
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    public async Task StopAsync()
    {
        if (_disposed)
            return;

        await _transitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _transitionLock.Wait();
            try
            {
                StopCoreAsync().GetAwaiter().GetResult();
            }
            finally
            {
                _transitionLock.Release();
            }
        }
        catch
        {
            // Dispose must still release managed synchronization resources.
        }
        finally
        {
            _disposed = true;
            _transitionLock.Dispose();
        }
    }

    private void RunLoop(CancellationToken cancellationToken)
    {
        var buffer = new double[ReadBlockSamples];
        Exception? fault = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IAnalogInputSession session = GetSessionOrThrow();
                int read = session.Read(buffer, ReadBlockSamples, ReadTimeoutSeconds);
                if (read <= 0 || cancellationToken.IsCancellationRequested)
                    continue;

                PressureScale scale;
                lock (_sync)
                    scale = _scale;

                double pressurePa = scale.ToPressurePa(buffer[read - 1]);
                Updated?.Invoke(this, new PreviewPressureUpdate(pressurePa));
            }
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                fault = ex;
        }
        finally
        {
            CleanupRun();
            if (fault is not null)
                Faulted?.Invoke(this, new PreviewFaultedEventArgs(fault));
        }
    }

    private async Task StopCoreAsync()
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

    private IAnalogInputSession GetSessionOrThrow()
    {
        lock (_sync)
            return _session ?? throw new InvalidOperationException("Preview DAQ session is not available.");
    }

    private void CleanupRun()
    {
        IAnalogInputSession? session;
        CancellationTokenSource? cts;

        lock (_sync)
        {
            session = _session;
            cts = _cts;
            _session = null;
            _cts = null;
            _runTask = null;
            _isRunning = false;
        }

        if (session is not null)
        {
            try { session.Stop(); }
            catch { }
            try { session.Dispose(); }
            catch { }
        }

        cts?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
