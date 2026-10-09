namespace DigitalManometerMonitor.Core.Measurement;

public sealed class MeasurementUpdate : EventArgs
{
    public MeasurementUpdate(long sampleCount, double elapsedSeconds, double latestPressurePa)
    {
        SampleCount = sampleCount;
        ElapsedSeconds = elapsedSeconds;
        LatestPressurePa = latestPressurePa;
    }

    public long SampleCount { get; }
    public double ElapsedSeconds { get; }
    public double LatestPressurePa { get; }
}
