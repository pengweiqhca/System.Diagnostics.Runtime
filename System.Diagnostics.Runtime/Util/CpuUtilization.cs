namespace System.Diagnostics.Runtime.Util;

internal static class CpuUtilization
{
    private static Interop.Sys.ProcessCpuInformation _prevCpuInfo;

    internal static double GetCpuUsage()
    {
        var cpuUtilization = Interop.Sys.GetCpuUtilization(out var cpuInfo);

        var totalProcTime = cpuInfo.lastRecordedKernelTime - _prevCpuInfo.lastRecordedKernelTime +
            (cpuInfo.lastRecordedUserTime - _prevCpuInfo.lastRecordedUserTime);

        var totalSystemTime = cpuInfo.lastRecordedCurrentTime - _prevCpuInfo.lastRecordedCurrentTime;

        // These may be 0 when we report CPU usage for the first time, in which case we should just return 0.
        var cpuUsage = _prevCpuInfo.lastRecordedCurrentTime != 0 && totalSystemTime != 0
            ? totalProcTime * 100.0 / totalSystemTime
            : cpuUtilization;

        _prevCpuInfo = cpuInfo;

        return Math.Min(100.0, Math.Round(cpuUsage / Environment.ProcessorCount, 2));
    }
}

internal static class ProcessTimes
{
    private static long _prevProcUserTime;
    private static long _prevProcKernelTime;
    private static long _prevSystemUserTime;
    private static long _prevSystemKernelTime;

    internal static double GetCpuUsage()
    {
        // Returns the current process' CPU usage as a percentage

        var cpuUsage = 0d;

        if (!Interop.Kernel32.GetProcessTimes(Interop.Kernel32.GetCurrentProcess(), out _, out _,
                out var procKernelTime, out var procUserTime) ||
            !Interop.Kernel32.GetSystemTimes(out _, out var systemUserTime, out var systemKernelTime)) return cpuUsage;

        var totalProcTime = procUserTime - _prevProcUserTime + (procKernelTime - _prevProcKernelTime);
        var totalSystemTime = systemUserTime - _prevSystemUserTime + (systemKernelTime - _prevSystemKernelTime);

        // These may be 0 when we report CPU usage for the first time, in which case we should just return 0.
        if (_prevSystemUserTime != 0 && _prevSystemKernelTime != 0 && totalSystemTime != 0)
            cpuUsage = totalProcTime * 100.0 / totalSystemTime;

        _prevProcUserTime = procUserTime;
        _prevProcKernelTime = procKernelTime;
        _prevSystemUserTime = systemUserTime;
        _prevSystemKernelTime = systemKernelTime;

        return Math.Min(100.0, Math.Round(cpuUsage, 2));
    }
}
