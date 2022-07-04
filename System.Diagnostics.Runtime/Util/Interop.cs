using System.Runtime.InteropServices;

namespace System.Diagnostics.Runtime.Util;

internal static class Interop
{
    internal static class Sys
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessCpuInformation
        {
            public readonly ulong lastRecordedCurrentTime;
            public readonly ulong lastRecordedKernelTime;
            public readonly ulong lastRecordedUserTime;
        }

        [DllImport("libSystem.Native", EntryPoint = "SystemNative_GetCpuUtilization")]
        internal static extern int GetCpuUtilization(out ProcessCpuInformation previousCpuInfo);
    }

    internal static class Kernel32
    {
        [DllImport("kernel32.dll")]
        internal static extern nint GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetProcessTimes(nint handleProcess, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    }
}
