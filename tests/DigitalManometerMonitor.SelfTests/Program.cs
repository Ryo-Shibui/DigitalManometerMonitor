using DigitalManometerMonitor.Core.Acquisition;
using DigitalManometerMonitor.Core.Measurement;
using DigitalManometerMonitor.Core.Storage;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Body)[]
        {
            ("Default pressure scale", TestDefaultPressureScale),
            ("USB-6000 hardware input range", TestHardwareInputRange),
            ("Timer sample count", TestTimerSampleCount),
            ("CSV writer refuses overwrite", TestCsvWriterRefusesOverwrite),
            ("CSV contains metadata and 1 ms timing", TestCsvContent),
            ("Preview monitor publishes scaled pressure and stops cleanly", TestPreviewMonitor)
        };

        int failures = 0;
        foreach ((string name, Action body) in tests)
        {
            try
            {
                body();
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL  {name}");
                Console.WriteLine(ex);
            }
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "All self-tests passed." : $"{failures} self-test(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void TestDefaultPressureScale()
    {
        var scale = new PressureScale(0, 10, 0, 100_000);
        AssertNear(0, scale.ToPressurePa(0), 1e-9);
        AssertNear(50_000, scale.ToPressurePa(5), 1e-9);
        AssertNear(100_000, scale.ToPressurePa(10), 1e-9);
    }

    private static void TestHardwareInputRange()
    {
        AssertNear(-10.0, NIDaqVoltageSession.HardwareMinimumVoltageV, 1e-12);
        AssertNear(10.0, NIDaqVoltageSession.HardwareMaximumVoltageV, 1e-12);
    }

    private static void TestTimerSampleCount()
    {
        string temp = Path.GetTempPath();
        var settings = MakeSettings(temp, "timer_test.csv") with
        {
            StopMode = MeasurementStopMode.Timer,
            TimerDurationSeconds = 2.345
        };

        AssertEqual(2345L, settings.GetTargetSampleCount()!.Value);
    }

    private static void TestCsvWriterRefusesOverwrite()
    {
        string dir = CreateTempDirectory();
        try
        {
            string path = Path.Combine(dir, "collision.csv");
            File.WriteAllText(path, "already here");
            var settings = MakeSettings(dir, "collision.csv");

            bool threw = false;
            try
            {
                using var _ = new CsvMeasurementWriter(path, settings, DateTimeOffset.Now);
            }
            catch (IOException)
            {
                threw = true;
            }

            AssertTrue(threw, "Expected FileMode.CreateNew collision to throw IOException.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void TestCsvContent()
    {
        string dir = CreateTempDirectory();
        try
        {
            var settings = MakeSettings(dir, "content.csv");
            string path = settings.GetOutputPath();
            DateTimeOffset start = new(2026, 10, 5, 14, 13, 0, TimeSpan.FromHours(9));
            using (var writer = new CsvMeasurementWriter(path, settings, start))
            {
                writer.WritePressureSamples(new[] { 1.25, 2.50, 3.75 }, 3, 0);
            }

            string text = File.ReadAllText(path);
            AssertTrue(text.Contains("# MeasurementStartLocal,2026-10-05T14:13:00.0000000+09:00"), "Missing start timestamp metadata.");
            AssertTrue(text.Contains("# TerminalConfiguration,RSE"), "Missing RSE terminal configuration metadata.");
            AssertTrue(text.Contains("# DAQHardwareInputMinimum_V,-10"), "Missing -10 V DAQ range metadata.");
            AssertTrue(text.Contains("# DAQHardwareInputMaximum_V,10"), "Missing +10 V DAQ range metadata.");
            AssertTrue(text.Contains("ElapsedTime_s,Pressure_Pa"), "Missing CSV header.");
            AssertTrue(text.Contains("0.000000,1.250000"), "Missing sample 0.");
            AssertTrue(text.Contains("0.001000,2.500000"), "Missing 1 ms sample timing.");
            AssertTrue(text.Contains("0.002000,3.750000"), "Missing 2 ms sample timing.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void TestPreviewMonitor()
    {
        var fake = new FakeAnalogInputSession(5.0);
        using var controller = new PreviewMonitorController(_ => fake);
        using var signal = new ManualResetEventSlim(false);

        double latest = double.NaN;
        controller.Updated += (_, update) =>
        {
            latest = update.LatestPressurePa;
            signal.Set();
        };

        controller.StartAsync("Dev1/ai0", new PressureScale(0, 10, 0, 100_000)).GetAwaiter().GetResult();
        AssertTrue(signal.Wait(TimeSpan.FromSeconds(2)), "Preview did not publish an update.");
        controller.StopAsync().GetAwaiter().GetResult();

        AssertNear(50_000.0, latest, 1e-9);
        AssertTrue(fake.Started, "Preview session was not started.");
        AssertTrue(fake.Stopped, "Preview session was not stopped.");
        AssertTrue(fake.Disposed, "Preview session was not disposed.");
        AssertTrue(!controller.IsRunning, "Preview controller still reports running after StopAsync.");
    }

    private static MeasurementSettings MakeSettings(string dir, string fileName) => new()
    {
        PhysicalChannel = "Dev1/ai0",
        OutputDirectory = dir,
        FileName = fileName,
        Scale = new PressureScale(0, 10, 0, 100_000),
        StopMode = MeasurementStopMode.Manual,
        TimerDurationSeconds = 60
    };

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DigitalManometerMonitorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void AssertNear(double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }

    private static void AssertEqual(long expected, long actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeAnalogInputSession : IAnalogInputSession
    {
        private readonly double _voltage;
        private bool _running;

        public FakeAnalogInputSession(double voltage)
        {
            _voltage = voltage;
        }

        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }

        public void Start()
        {
            Started = true;
            _running = true;
        }

        public int Read(double[] destination, int samplesRequested, double timeoutSeconds)
        {
            if (!_running)
                throw new InvalidOperationException("Fake session is not running.");

            int count = Math.Min(samplesRequested, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = _voltage;

            Thread.Sleep(2);
            return count;
        }

        public void Stop()
        {
            Stopped = true;
            _running = false;
        }

        public void Dispose()
        {
            Disposed = true;
            _running = false;
        }
    }
}
