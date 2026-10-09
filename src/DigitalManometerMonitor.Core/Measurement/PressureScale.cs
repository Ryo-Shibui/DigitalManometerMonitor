namespace DigitalManometerMonitor.Core.Measurement;

public readonly record struct PressureScale(
    double VoltageLowV,
    double VoltageHighV,
    double PressureAtLowPa,
    double PressureAtHighPa)
{
    public double ToPressurePa(double voltageV)
    {
        if (!double.IsFinite(voltageV))
            throw new ArgumentOutOfRangeException(nameof(voltageV), "Voltage must be finite.");

        double spanV = VoltageHighV - VoltageLowV;
        if (!double.IsFinite(spanV) || spanV <= 0.0)
            throw new InvalidOperationException("Voltage High must be greater than Voltage Low.");

        double slope = (PressureAtHighPa - PressureAtLowPa) / spanV;
        return PressureAtLowPa + (voltageV - VoltageLowV) * slope;
    }

    public void Validate()
    {
        if (!double.IsFinite(VoltageLowV) || !double.IsFinite(VoltageHighV) ||
            !double.IsFinite(PressureAtLowPa) || !double.IsFinite(PressureAtHighPa))
        {
            throw new ArgumentException("Scale values must be finite numbers.");
        }

        if (VoltageHighV <= VoltageLowV)
            throw new ArgumentException("Voltage High must be greater than Voltage Low.");

        if (PressureAtHighPa == PressureAtLowPa)
            throw new ArgumentException("The two pressure calibration values must be different.");
    }
}
