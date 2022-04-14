using System.Runtime.InteropServices;

namespace System.Diagnostics.Runtime.Util;

internal static class Interop
{
    internal static class Sys
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessCpuInformation
        {
            private readonly ulong lastRecordedCurrentTime;
            private readonly ulong lastRecordedKernelTime;
            private readonly ulong lastRecordedUserTime;
        }

        [DllImport("libSystem.Native", EntryPoint = "SystemNative_GetCpuUtilization")]
        internal static extern int GetCpuUtilization(ref ProcessCpuInformation previousCpuInfo);
    }

    internal static class Kernel32
    {
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetProcessTimes(IntPtr handleProcess, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    }
}
