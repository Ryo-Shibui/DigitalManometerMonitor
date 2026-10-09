using System.Runtime.InteropServices;
using System.Text;

namespace DigitalManometerMonitor.Core.Acquisition;

internal static class NIDaqmxNative
{
    private const string DllName = "nicaiu.dll";

    internal const int ValRse = 10083;
    internal const int ValVolts = 10348;
    internal const int ValRising = 10280;
    internal const int ValContinuousSamples = 10123;
    internal const uint ValGroupByChannel = 0;

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxGetSysDevNames(StringBuilder data, uint bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxGetDevAIPhysicalChans(
        string device,
        StringBuilder data,
        uint bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxCreateTask(
        string taskName,
        out IntPtr taskHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxCreateAIVoltageChan(
        IntPtr taskHandle,
        string physicalChannel,
        string nameToAssignToChannel,
        int terminalConfig,
        double minVal,
        double maxVal,
        int units,
        string? customScaleName);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxCfgSampClkTiming(
        IntPtr taskHandle,
        string source,
        double rate,
        int activeEdge,
        int sampleMode,
        ulong samplesPerChannelToAcquire);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int DAQmxStartTask(IntPtr taskHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int DAQmxReadAnalogF64(
        IntPtr taskHandle,
        int numberOfSamplesPerChannel,
        double timeout,
        uint fillMode,
        [Out] double[] readArray,
        uint arraySizeInSamples,
        out int samplesPerChannelRead,
        IntPtr reserved);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int DAQmxStopTask(IntPtr taskHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int DAQmxClearTask(IntPtr taskHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxGetExtendedErrorInfo(StringBuilder errorString, uint bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    internal static extern int DAQmxGetErrorString(int errorCode, StringBuilder errorString, uint bufferSize);

    internal static void Check(int status)
    {
        if (status >= 0)
            return;

        throw new NIDaqException(status, GetErrorText(status));
    }

    private static string GetErrorText(int status)
    {
        try
        {
            var extended = new StringBuilder(4096);
            int extendedStatus = DAQmxGetExtendedErrorInfo(extended, (uint)extended.Capacity);
            if (extendedStatus >= 0 && extended.Length > 0)
                return extended.ToString().Trim();

            var basic = new StringBuilder(2048);
            int basicStatus = DAQmxGetErrorString(status, basic, (uint)basic.Capacity);
            if (basicStatus >= 0 && basic.Length > 0)
                return basic.ToString().Trim();
        }
        catch
        {
            // Preserve the original NI-DAQmx status even if error-text retrieval fails.
        }

        return "Unknown NI-DAQmx error.";
    }
}
