namespace System.Diagnostics.Runtime.Util;

internal static class CpuUtilization
{
    private static Interop.Sys.ProcessCpuInformation _cpuInfo;

    internal static int GetCpuUsage() =>
        Interop.Sys.GetCpuUtilization(ref _cpuInfo) / Environment.ProcessorCount;
}

internal static class ProcessTimes
{
    private static long _prevProcUserTime;
    private static long _prevProcKernelTime;
    private static long _prevSystemUserTime;
    private static long _prevSystemKernelTime;

    internal static int GetCpuUsage()
    {
        // Returns the current process' CPU usage as a percentage

        var cpuUsage = 0;

        if (!Interop.Kernel32.GetProcessTimes(Interop.Kernel32.GetCurrentProcess(), out _, out _, out var procKernelTime, out var procUserTime) ||
            !Interop.Kernel32.GetSystemTimes(out _, out var systemUserTime, out var systemKernelTime)) return cpuUsage;

        var totalProcTime = procUserTime - _prevProcUserTime + (procKernelTime - _prevProcKernelTime);
        var totalSystemTime = systemUserTime - _prevSystemUserTime + (systemKernelTime - _prevSystemKernelTime);

        // These may be 0 when we report CPU usage for the first time, in which case we should just return 0.
        if (_prevSystemUserTime != 0 && _prevSystemKernelTime != 0 && totalSystemTime != 0)
            cpuUsage = (int)(totalProcTime * 100 / totalSystemTime);

        _prevProcUserTime = procUserTime;
        _prevProcKernelTime = procKernelTime;
        _prevSystemUserTime = systemUserTime;
        _prevSystemKernelTime = systemKernelTime;

        return cpuUsage;
    }
}
