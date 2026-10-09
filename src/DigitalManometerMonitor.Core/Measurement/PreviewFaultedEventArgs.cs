namespace DigitalManometerMonitor.Core.Measurement;

public sealed class PreviewFaultedEventArgs : EventArgs
{
    public PreviewFaultedEventArgs(Exception exception)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public Exception Exception { get; }
}
