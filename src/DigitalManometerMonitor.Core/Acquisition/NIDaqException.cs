namespace DigitalManometerMonitor.Core.Acquisition;

public sealed class NIDaqException : Exception
{
    public NIDaqException(int statusCode, string message)
        : base($"NI-DAQmx error {statusCode}: {message}")
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
