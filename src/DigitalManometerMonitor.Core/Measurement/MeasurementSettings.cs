namespace DigitalManometerMonitor.Core.Measurement;

public sealed record MeasurementSettings
{
    public const double SampleRateHz = 1000.0;

    public required string PhysicalChannel { get; init; }
    public required string OutputDirectory { get; init; }
    public required string FileName { get; init; }
    public required PressureScale Scale { get; init; }
    public MeasurementStopMode StopMode { get; init; } = MeasurementStopMode.Manual;
    public double TimerDurationSeconds { get; init; } = 60.0;

    public string GetOutputPath()
    {
        ValidateFileNameOnly();
        string fileName = FileName.Trim();
        if (Path.GetExtension(fileName).Length == 0)
            fileName += ".csv";
        else if (!string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("File name must use the .csv extension.");

        return Path.Combine(OutputDirectory.Trim(), fileName);
    }

    public long? GetTargetSampleCount()
    {
        if (StopMode != MeasurementStopMode.Timer)
            return null;

        double rawSamples = TimerDurationSeconds * SampleRateHz;
        if (!double.IsFinite(rawSamples) || rawSamples < 1.0 || rawSamples > long.MaxValue)
            throw new ArgumentException("Timer duration is outside the supported range.");

        return Math.Max(1L, checked((long)Math.Round(rawSamples, MidpointRounding.AwayFromZero)));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PhysicalChannel))
            throw new ArgumentException("Select an analog input channel.");

        if (string.IsNullOrWhiteSpace(OutputDirectory))
            throw new ArgumentException("Select an output directory.");

        if (!Directory.Exists(OutputDirectory.Trim()))
            throw new DirectoryNotFoundException("The selected output directory does not exist.");

        ValidateFileNameOnly();
        Scale.Validate();

        if (StopMode == MeasurementStopMode.Timer)
        {
            if (!double.IsFinite(TimerDurationSeconds) || TimerDurationSeconds <= 0.0)
                throw new ArgumentException("Timer duration must be greater than 0 s.");

            _ = GetTargetSampleCount();
        }
    }

    private void ValidateFileNameOnly()
    {
        if (string.IsNullOrWhiteSpace(FileName))
            throw new ArgumentException("Enter a file name.");

        string trimmed = FileName.Trim();
        if (!string.Equals(trimmed, Path.GetFileName(trimmed), StringComparison.Ordinal))
            throw new ArgumentException("Enter only a file name. Select the directory separately.");

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The file name contains invalid characters.");
    }
}
