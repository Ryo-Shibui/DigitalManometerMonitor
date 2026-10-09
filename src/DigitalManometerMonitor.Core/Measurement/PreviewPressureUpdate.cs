namespace DigitalManometerMonitor.Core.Measurement;

public sealed class PreviewPressureUpdate : EventArgs
{
    public PreviewPressureUpdate(double latestPressurePa)
    {
        LatestPressurePa = latestPressurePa;
    }

    public double LatestPressurePa { get; }
}
