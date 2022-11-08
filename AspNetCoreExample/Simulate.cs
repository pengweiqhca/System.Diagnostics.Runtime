// ReSharper disable once CheckNamespace

using System.Linq.Expressions;

namespace System.Diagnostics.Runtime;

public static class Simulate
{
    private static readonly object LockObj = new();

    public static async Task Invoke(
        bool simulateAlloc = true,
        bool simulateContention = true,
        bool simulateJit = true,
        bool simulateException = true,
        bool simulateBlocking = true,
        Func<HttpClient>? simulateOutgoingNetwork = null)
    {
        GC.Collect(2, GCCollectionMode.Forced);

        var r = new Random();
        if (simulateAlloc)
        {
            // assign some SOH memory
            var x = new byte[r.Next(1024, 1024 * 64)];

            // assign some LOH memory
            x = new byte[r.Next(1024 * 90, 1024 * 100)];
        }

        // await a task (will result in a Task being scheduled on the thread pool)
        await Task.Yield();

        if (simulateContention)
            lock (LockObj)
            {
                Thread.Sleep(100);
            }

        if (simulateJit)
        {
            var val = r.Next();
            CompileMe(() => val);
        }

        if (simulateException)
            try
            {
                var divide = 0;
                var result = 1 / divide;
            }
            catch
            {
            }

        if (simulateBlocking) Thread.Sleep(100);

        if (simulateOutgoingNetwork != null)
        {
            using var _ = await simulateOutgoingNetwork().GetAsync("https://httpstat.us/200").ConfigureAwait(false);
        }
    }

    private static void CompileMe(Expression<Func<int>> func)
    {
        func.Compile()();
    }
}
