using System.Text;

namespace DigitalManometerMonitor.Core.Acquisition;

public static class NIDaqChannelDiscovery
{
    public static IReadOnlyList<string> GetAnalogInputChannels()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NI-DAQmx acquisition is supported only on Windows.");

        var deviceBuffer = new StringBuilder(4096);
        NIDaqmxNative.Check(NIDaqmxNative.DAQmxGetSysDevNames(deviceBuffer, (uint)deviceBuffer.Capacity));

        var result = new List<string>();
        foreach (string device in SplitCsv(deviceBuffer.ToString()))
        {
            var channelBuffer = new StringBuilder(32768);
            NIDaqmxNative.Check(NIDaqmxNative.DAQmxGetDevAIPhysicalChans(
                device,
                channelBuffer,
                (uint)channelBuffer.Capacity));

            result.AddRange(SplitCsv(channelBuffer.ToString()));
        }

        return result;
    }

    private static IEnumerable<string> SplitCsv(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
