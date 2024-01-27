using NUnit.Framework;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

internal class Enabled_For_ThreadPoolStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() =>
        new() { ThreadingEnabled = true, EnabledNativeRuntime = true };
#if !NET7_0_OR_GREATER
    [Test]
    public Task When_IO_work_is_executed_on_the_thread_pool_then_the_number_of_io_threads_is_measured()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Cannot run this test on non-windows platforms.");

        return InstrumentTest.Assert(async () =>
            {
                // need to schedule a bunch of IO work to make the IO pool grow
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

                var httpTasks = Enumerable.Range(1, 10)
                    .Select(_ => client.GetAsync("https://www.bing.com"));

                await Task.WhenAll(httpTasks).ConfigureAwait(false);
            }, measurements =>
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}threadpool.active.io.thread.count"), Is.GreaterThanOrEqualTo(1).After(5000, 10)),
            $"{Options.MetricPrefix}threadpool.active.io.thread.count");
    }
#endif
    [Test]
    public Task When_blocking_work_is_executed_on_the_thread_pool()
    {
        var sleepDelay = TimeSpan.FromMilliseconds(250);
        var desiredSecondsToBlock = 5;
        var numTasksToSchedule = (int)(Environment.ProcessorCount / sleepDelay.TotalSeconds) * desiredSecondsToBlock;

        return InstrumentTest.Assert(() => Enumerable.Range(1, numTasksToSchedule)
                .Select(_ => Task.Run(() => Thread.Sleep(sleepDelay)))
                .ToArray(), measurements =>
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}threadpool.active.worker.thread.count"), Is.GreaterThan(Environment.ProcessorCount).After(desiredSecondsToBlock * 1000, 10)),
            $"{Options.MetricPrefix}threadpool.active.worker.thread.count");
    }
#if NET
    [Test]
    public Task When_work_is_executed_on_the_thread_pool_then_executed_work_is_measured()
    {
        const int numTasksToSchedule = 100;

        return InstrumentTest.Assert(() => Task.WhenAll(Enumerable.Range(1, numTasksToSchedule)
                .Select(_ => Task.Run(() => { }))),
            measurements =>
            {
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}threadpool.thread.count"), Is.GreaterThanOrEqualTo(Environment.ProcessorCount).After(2_000, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}threadpool.completed.items.total"), Is.GreaterThanOrEqualTo(numTasksToSchedule).After(2_000, 10));
            }, $"{Options.MetricPrefix}threadpool.thread.count",
            $"{Options.MetricPrefix}threadpool.completed.items.total");
    }

    [Test]
    public Task When_timers_are_active_then_they_are_measured()
    {
        const int numTimersToSchedule = 100;

        return InstrumentTest.Assert(() => Enumerable.Range(1, numTimersToSchedule)
                .Select(n => Task.Delay(3000 + n))
                .ToArray(),
            measurements =>
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}threadpool.timer.count"), Is.GreaterThanOrEqualTo(numTimersToSchedule).After(2_000, 10)), $"{Options.MetricPrefix}threadpool.timer.count");
    }

    [Test]
    public Task When_blocking_work_is_executed_on_the_thread_pool_then_thread_pool_delays_are_measured()
    {
        var sleepDelay = TimeSpan.FromMilliseconds(250);
        var desiredSecondsToBlock = 5;
        var numTasksToSchedule = (int)(Environment.ProcessorCount / sleepDelay.TotalSeconds) * desiredSecondsToBlock;

        return InstrumentTest.Assert(() => Enumerable.Range(1, numTasksToSchedule)
                .Select(_ => Task.Run(() => Thread.Sleep(sleepDelay)))
                .ToArray(), measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}threadpool.queue.length"), Is.GreaterThan(0).After(desiredSecondsToBlock * 1000, 10)),
            $"{Options.MetricPrefix}threadpool.queue.length");
    }
#endif
}
#if !NET7_0_OR_GREATER
internal class Given_Native_Runtime_Are_Enabled_For_ThreadPoolStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { ThreadingEnabled = true, EnabledNativeRuntime = true };
#if NETFRAMEWORK
    [Test]
    public Task When_work_is_executed_on_the_thread_pool_then_executed_work_is_measured()
    {
        const int numTasksToSchedule = 100;

        return InstrumentTest.Assert(() => Task.WhenAll(Enumerable.Range(1, numTasksToSchedule)
                .Select(_ => Task.Run(() => { }))),
            measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}threadpool.completed.items.total"), Is.GreaterThanOrEqualTo(numTasksToSchedule).After(2_000, 10)),
            $"{Options.MetricPrefix}threadpool.completed.items.total");
    }

    [Test]
    public Task When_blocking_work_is_executed_on_the_thread_pool_then_thread_pool_delays_are_measured()
    {
        var sleepDelay = TimeSpan.FromMilliseconds(250);
        var desiredSecondsToBlock = 5;
        var numTasksToSchedule = (int)(Environment.ProcessorCount / sleepDelay.TotalSeconds) * desiredSecondsToBlock;

        return InstrumentTest.Assert(() => Enumerable.Range(1, numTasksToSchedule)
            .Select(_ => Task.Run(() => Thread.Sleep(sleepDelay)))
            .ToArray(), measurements =>
            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}threadpool.queue.length"), Is.GreaterThanOrEqualTo(numTasksToSchedule).After(desiredSecondsToBlock * 1000, 10)),
            $"{Options.MetricPrefix}threadpool.queue.length");
    }
#elif NET6
    [Test]
    public Task When_work_is_sleep_on_the_thread_pool_then_executed_work_is_measured() =>
        InstrumentTest.Assert(() =>
        {
            // schedule a bunch of blocking tasks that will make the thread pool will grow
            return Task.WhenAll(Enumerable.Range(1, 1000)
                .Select(_ => Task.Run(() => Thread.Sleep(200))));
        }, measurements =>
        {
            Assert.NotNull(measurements);

            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}threadpool.adjustments.total"), Is.GreaterThanOrEqualTo(1));
        }, $"{Options.MetricPrefix}threadpool.adjustments.total");
#endif
    [Test]
    public Task When_IO_work_is_executed_on_the_thread_pool_then_the_number_of_io_threads_is_measured()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Cannot run this test on non-windows platforms.");

        return InstrumentTest.Assert(async () =>
            {
                // need to schedule a bunch of IO work to make the IO pool grow
                using var client = new HttpClient();

                var httpTasks = Enumerable.Range(1, 50)
                    .Select(_ => client.GetAsync("https://www.bing.com"));

                await Task.WhenAll(httpTasks).ConfigureAwait(false);
            }, measurements =>
                Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}threadpool.io.thread.count"), Is.GreaterThanOrEqualTo(1).After(30000, 10)),
            $"{Options.MetricPrefix}threadpool.io.thread.count");
    }
}
#endif
