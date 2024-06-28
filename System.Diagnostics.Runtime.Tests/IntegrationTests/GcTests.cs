using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

internal class Given_Are_Available_For_GcStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { GcEnabled = true };
#if NET
    [Test]
    public Task Computer_memory_available() =>
        InstrumentTest.Assert(measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.available_memory.size"),
            Is.GreaterThan(0.0).After(2000, 10)), $"{Options.MetricPrefix}gc.available_memory.size");
#else
    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_heap_sizes_are_updated() =>
        InstrumentTest.Assert(measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.objects.size"),
            Is.GreaterThan(0.0).After(2000, 10)), $"{Options.MetricPrefix}gc.objects.size");
#endif
    [Test]
    public Task When_collections_happen_then_the_collection_count_is_increased([Values(0, 1, 2)] int generation)
    {
        const int numCollectionsToRun = 10;

        return InstrumentTest.Assert(() =>
            {
                // run collections
                for (var i = 0; i < numCollectionsToRun; i++) GC.Collect(generation, GCCollectionMode.Forced, true);
            }, measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.collections.count", "generation", "gen" + generation),
                Is.GreaterThanOrEqualTo(numCollectionsToRun).After(2_000, 10)),
            $"{Options.MetricPrefix}gc.collections.count");
    }
}

internal class Given_Native_Runtime_Are_Available_For_GcStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { GcEnabled = true, EnabledNativeRuntime = true };
#if NETFRAMEWORK
    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_gc_fragmentation_can_be_calculated() =>
        InstrumentTest.Assert(() =>
            {
                _ = new byte[1024 * 1024 * 10];

                GC.Collect(0, GCCollectionMode.Forced);
            },
            measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.heap.fragmentation.size"),
                Is.GreaterThan(0.0).After(10000, 100)), $"{Options.MetricPrefix}gc.heap.fragmentation.size");
#endif
    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_heap_sizes_are_updated() =>
        InstrumentTest.Assert(() =>
            {
                unsafe
                {
                    // arrange (fix a variable to ensure the pinned objects counter is incremented
                    var b = new byte[1];
                    fixed (byte* p = b)
                    {
                        // act
                        GC.Collect(0);
                    }
                }
            }, measurements =>
            {
#if NETFRAMEWORK
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "generation", "gen0"),
                    Is.GreaterThan(0).After(5000, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "generation", "gen1"),
                    Is.GreaterThan(0).After(5000, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "generation", "gen2"),
                    Is.GreaterThan(0).After(5000, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "generation", "loh"),
                    Is.GreaterThan(0).After(5000, 10));
#endif
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.pinned.objects"),
                    Is.GreaterThan(0).After(5000, 10));
#if NETFRAMEWORK
            }, $"{Options.MetricPrefix}gc.heap.size",
#else
            },
#endif
            $"{Options.MetricPrefix}gc.pinned.objects");

    [Test]
    public Task When_collections_happen_then_the_collection_count_is_increased([Values(0, 1, 2)] int generation)
    {
        const int numCollectionsToRun = 10;

        return InstrumentTest.Assert(() =>
        {
            Thread.Sleep(2000);

            // run collections
            for (var i = 0; i < numCollectionsToRun; i++) GC.Collect(generation, GCCollectionMode.Forced);
        }, measurements =>
        {
            // For some reason, the full number of gen0 collections are not being collected. I expect this is because .NET will not always force
            // a gen 0 collection to occur.
            const int minExpectedCollections = numCollectionsToRun / 2;

            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.collections.count", "generation", "gen" + generation),
                Is.GreaterThanOrEqualTo(minExpectedCollections).After(5000, 10));

            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.reasons.count"),
                Is.GreaterThanOrEqualTo(minExpectedCollections).After(5000, 10));
        }, $"{Options.MetricPrefix}gc.collections.count", $"{Options.MetricPrefix}gc.reasons.count");
    }

    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_finalization_queue_is_updated() =>
        InstrumentTest.Assert(() =>
            {
                // arrange
                {
                    var finalizable = new FinalizableTest();
                    finalizable = null;
                }
                {
                    var finalizable = new FinalizableTest();
                    finalizable = null;
                }
                {
                    var finalizable = new FinalizableTest();
                    finalizable = null;
                }

                GC.Collect(0);
            }, measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.finalization.queue.length"),
                Is.GreaterThan(0).After(5000, 10)),
            $"{Options.MetricPrefix}gc.finalization.queue.length");

    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_collection_and_pause_stats_and_reasons_are_updated() =>
        InstrumentTest.Assert(() =>
            {
                // arrange
                GC.Collect(1, GCCollectionMode.Forced);
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }, measurements =>
            {
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.duration"), Is.GreaterThan(0).After(5000, 10)); // at least 3 generations
                Assert.That(() => measurements.Values($"{Options.MetricPrefix}gc.reasons.count"), Is.All.GreaterThan(0));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.collections.duration"), Is.GreaterThan(0).After(5000, 10));
            }, $"{Options.MetricPrefix}gc.duration",
            $"{Options.MetricPrefix}gc.reasons.count",
            $"{Options.MetricPrefix}gc.collections.duration");

    [Test]
    public Task When_100kb_of_small_objects_are_allocated_then_the_allocated_bytes_counter_is_increased() =>
        InstrumentTest.Assert(() =>
            {
                // allocate roughly 100kb+ of small objects
                for (var i = 0; i < 11; i++) _ = new byte[10_000];
            }, measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap_allocations.size", "heap", "soh"), Is.GreaterThanOrEqualTo(100_000).After(2_000, 10)),
            $"{Options.MetricPrefix}gc.heap_allocations.size");

    [Test]
    public Task When_a_100kb_large_object_is_allocated_then_the_allocated_bytes_counter_is_increased() =>
        InstrumentTest.Assert(() =>
            {
                // allocate roughly 100kb+ of small objects
                for (var i = 0; i < 11; i++) _ = new byte[100_000];
            }, measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap_allocations.size", "heap", "loh"), Is.GreaterThanOrEqualTo(100_0000).After(2_000, 10)),
            $"{Options.MetricPrefix}gc.heap_allocations.size");

    private class FinalizableTest
    {
        ~FinalizableTest()
        {
            // Sleep for a bit so our object won't exit the finalization queue immediately
            Thread.Sleep(1000);
        }
    }
}
