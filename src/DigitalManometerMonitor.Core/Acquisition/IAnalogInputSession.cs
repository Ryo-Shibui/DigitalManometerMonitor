namespace DigitalManometerMonitor.Core.Acquisition;

public interface IAnalogInputSession : IDisposable
{
    void Start();
    int Read(double[] destination, int samplesRequested, double timeoutSeconds);
    void Stop();
}
