using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

internal class Given_Are_Available_For_GcStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { GcEnabled = true };
#if NETCOREAPP
    [Test]
    public Task When_objects_are_allocated_then_the_allocated_bytes_counter_is_increased() =>
        InstrumentTest.Assert(() =>
            {
                // allocate roughly 100kb+ of small objects
                for (var i = 0; i < 11; i++)
                {
                    _ = new byte[10_000];
                }
            }, measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.allocated.total"), Is.GreaterThanOrEqualTo(100_000).After(2_000, 10)),
            $"{Options.MetricPrefix}gc.allocated.total");

    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_gc_fragmentation_can_be_calculated() =>
        InstrumentTest.Assert(() => GC.Collect(0, GCCollectionMode.Forced),
            measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.fragmentation"),
                Is.GreaterThan(0.0).After(2000, 10)), $"{Options.MetricPrefix}gc.fragmentation");

    [Test]
    public Task Computer_memory_available() =>
        InstrumentTest.Assert(measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.memory.total.available"),
            Is.GreaterThan(0.0).After(2000, 10)), $"{Options.MetricPrefix}gc.memory.total.available");
#endif
    [Test]
    public Task When_collections_happen_then_the_collection_count_is_increased([Values(0, 1, 2)] int generation)
    {
        const int numCollectionsToRun = 10;

        return InstrumentTest.Assert(() =>
            {
                // run collections
                for (var i = 0; i < numCollectionsToRun; i++)
                {
                    GC.Collect(generation, GCCollectionMode.Forced);
                }
            }, measurements => Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.collection.total", "gc_generation", generation.ToString()),
                Is.GreaterThanOrEqualTo(numCollectionsToRun).After(2_000, 10)),
            $"{Options.MetricPrefix}gc.collection.total");
    }

    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_heap_sizes_are_updated() =>
        InstrumentTest.Assert(measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.heap.size"),
            Is.GreaterThan(0.0).After(2000, 10)), $"{Options.MetricPrefix}gc.heap.size");
}
#if NETCOREAPP
internal class Given_Only_Counters_Are_Available_For_GcStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { GcEnabled = true, EnabledSystemRuntime = true };

    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_heap_sizes_are_updated() =>
        InstrumentTest.Assert(measurements =>
        {
            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "0"),
                Is.GreaterThan(0).After(2000, 10));
            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "1"),
                Is.GreaterThan(0).After(2000, 10));
            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "2"),
                Is.GreaterThan(0).After(2000, 10));
            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "loh"),
                Is.GreaterThan(0).After(2000, 10));
        }, $"{Options.MetricPrefix}gc.heap.size");

    [Test]
    public Task When_a_garbage_collection_is_performed_then_the_pause_ratios_can_be_calculated() =>
        InstrumentTest.Assert(() =>
        {
            // arrange
            for (var i = 0; i < 5; i++)
                GC.Collect(2, GCCollectionMode.Forced, true, true);
        }, measurements => Assert.That(() => measurements.LastValue($"{Options.MetricPrefix}gc.pause.ratio"),
            Is.GreaterThan(0.0).After(2000, 10)), $"{Options.MetricPrefix}gc.pause.ratio");
}

internal class Given_Gc_Info_Events_Are_Available_For_GcStats : IntegrationTestBase
{
    protected override RuntimeMetricsOptions GetOptions() => new() { GcEnabled = true, EnabledNativeRuntime = true };

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
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "0"),
                    Is.GreaterThan(0).After(200, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "1"),
                    Is.GreaterThan(0).After(200, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "2"),
                    Is.GreaterThan(0).After(200, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.heap.size", "gc_generation", "loh"),
                    Is.GreaterThan(0).After(200, 10));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.pinned.objects"),
                    Is.GreaterThan(0).After(200, 10));
            }, $"{Options.MetricPrefix}gc.heap.size",
            $"{Options.MetricPrefix}gc.pinned.objects");

    [Test]
    public Task When_collections_happen_then_the_collection_count_is_increased([Values(0, 1, 2)] int generation)
    {
        const int numCollectionsToRun = 10;

        return InstrumentTest.Assert(() =>
        {
            Thread.Sleep(2000);

            // run collections
            for (var i = 0; i < numCollectionsToRun; i++)
            {
                GC.Collect(generation, GCCollectionMode.Forced);
            }
        }, measurements =>
        {
            // For some reason, the full number of gen0 collections are not being collected. I expect this is because .NET will not always force
            // a gen 0 collection to occur.
            const int minExpectedCollections = numCollectionsToRun / 2;

            Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.collection.total", "gc_generation", generation.ToString()),
                Is.GreaterThanOrEqualTo(minExpectedCollections).After(2_000, 10)
            );
        }, $"{Options.MetricPrefix}gc.collection.total");
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
            Is.GreaterThan(0).After(200, 10)),
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
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.collection.time"), Is.GreaterThan(0).After(500, 10)); // at least 3 generations
                Assert.That(() => measurements.Values($"{Options.MetricPrefix}gc.collection.time"), Is.All.GreaterThan(0));
                Assert.That(() => measurements.Values($"{Options.MetricPrefix}gc.collection.total"), Is.All.GreaterThan(0));
                Assert.That(() => measurements.Sum($"{Options.MetricPrefix}gc.pause.time"), Is.GreaterThan(0).After(500, 10));
            }, $"{Options.MetricPrefix}gc.collection.time",
            $"{Options.MetricPrefix}gc.collection.total",
            $"{Options.MetricPrefix}gc.pause.time");

    private class FinalizableTest
    {
        ~FinalizableTest()
        {
            // Sleep for a bit so our object won't exit the finalization queue immediately
            Thread.Sleep(1000);
        }
    }
}
#endif
