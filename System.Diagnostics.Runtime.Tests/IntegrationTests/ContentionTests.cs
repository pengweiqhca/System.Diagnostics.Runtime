using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

[TestFixture]
internal class Given_Contention_Events_Are_Enabled_For_Contention_Stats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { ContentionEnabled = true, EnabledNativeRuntime = true };

    [Test]
    public Task Will_measure_no_contention_on_an_uncontested_lock() =>
        InstrumentTest.Assert(() =>
            {
                // arrange
                var key = new object();

                // act
                lock (key)
                {
                }
            }, Assert.IsEmpty, $"{Options.MetricPrefix}monitor.lock_contention.count",
            $"{Options.MetricPrefix}monitor.lock_contention.duration");

    /// <summary>
    /// This test has the potential to be flaky (due to attempting to simulate lock contention across multiple threads in the thread pool),
    /// may have to revisit this in the future..
    /// </summary>
    /// <returns></returns>
    [Test]
    public Task Will_measure_contention_on_a_contested_lock()
    {
        // arrange
        const int numThreads = 10;
        const int sleepForMs = 50;

        return InstrumentTest.Assert(() =>
            {
                var key = new object();
                // Increase the min. thread pool size so that when we use Thread.Sleep, we don't run into scheduling delays
                ThreadPool.SetMinThreads(numThreads * 2, numThreads * 2);

                return Task.WhenAll(Enumerable.Range(1, numThreads)
                    .Select(_ => Task.Run(() =>
                    {
                        Console.WriteLine(Process.GetCurrentProcess().Threads[Thread.CurrentThread.ManagedThreadId - 1].Id);
                        lock (key)
                        {
                            Thread.Sleep(sleepForMs);
                        }
                    })));
            }, measurements =>
            {
                // Why -1? The first thread will not contend the lock
                const int numLocksContended = numThreads - 1;

                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}monitor.lock_contention.count"),
                    Is.GreaterThan(numLocksContended).After(3000, 100));

                // Pattern of expected contention times is: 50ms, 100ms, 150ms, etc.
                var expectedDelay = TimeSpan.FromMilliseconds(Enumerable.Range(1, numLocksContended).Aggregate(sleepForMs, (acc, next) => acc + sleepForMs * next));

                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}monitor.lock_contention.duration"),
                    Is.GreaterThan(expectedDelay.TotalSeconds));
            }, $"{Options.MetricPrefix}monitor.lock_contention.count",
            $"{Options.MetricPrefix}monitor.lock_contention.duration");
    }
}
