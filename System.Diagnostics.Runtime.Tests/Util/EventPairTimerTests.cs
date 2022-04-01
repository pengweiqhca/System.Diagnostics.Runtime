using System.Diagnostics.Runtime.Tests.EventListening;
using System.Diagnostics.Runtime.Util;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.Util;

[TestFixture]
public class Given_An_EventPairTimer_That_Samples_Every_Event : EventPairTimerBaseClass
{
    private EventPairTimer<long> _eventPairTimer = default!;

    [SetUp]
    public void SetUp()
    {
        _eventPairTimer = new EventPairTimer<long>(EventIdStart, EventIdEnd, x => (long)x.Payload![0]!);
    }

    [Test]
    public void TryGetEventPairDuration_ignores_events_that_its_not_configured_to_look_for()
    {
        var nonMonitoredEvent = TestHelpers.CreateEventWrittenEventArgs(3);
        Assert.That(_eventPairTimer.TryGetDuration(nonMonitoredEvent, out var duration), Is.EqualTo(DurationResult.Unrecognized));
        Assert.That(duration, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void TryGetEventPairDuration_ignores_end_events_if_it_never_saw_the_start_event()
    {
        var nonMonitoredEvent = TestHelpers.CreateEventWrittenEventArgs(EventIdEnd, payload: 1L);
        Assert.That(_eventPairTimer.TryGetDuration(nonMonitoredEvent, out var duration), Is.EqualTo(DurationResult.FinalWithoutDuration));
        Assert.That(duration, Is.EqualTo(TimeSpan.Zero));
    }
#if NETCOREAPP
    [Test]
    public void TryGetEventPairDuration_calculates_duration_between_configured_events()
    {
        // arrange
        var now = DateTime.UtcNow;
        var startEvent = TestHelpers.CreateEventWrittenEventArgs(EventIdStart, now, payload: 1L);
        Assert.That(_eventPairTimer.TryGetDuration(startEvent, out _), Is.EqualTo(DurationResult.Start));
        var endEvent = TestHelpers.CreateEventWrittenEventArgs(EventIdEnd, now.AddMilliseconds(100), payload: 1L);

        // act
        Assert.That(_eventPairTimer.TryGetDuration(endEvent, out var duration), Is.EqualTo(DurationResult.FinalWithDuration));
        Assert.That(duration.TotalMilliseconds, Is.EqualTo(100));
    }
#endif
    [Test]
    public void TryGetEventPairDuration_calculates_duration_between_configured_events_that_occur_simultaneously()
    {
        // arrange
        var now = DateTime.UtcNow;
        var startEvent = TestHelpers.CreateEventWrittenEventArgs(EventIdStart, now, payload: 1L);
        Assert.That(_eventPairTimer.TryGetDuration(startEvent, out _), Is.EqualTo(DurationResult.Start));
        var endEvent = TestHelpers.CreateEventWrittenEventArgs(EventIdEnd, now, payload: 1L);

        // act
        Assert.That(_eventPairTimer.TryGetDuration(endEvent, out var duration), Is.EqualTo(DurationResult.FinalWithDuration));
#if NETCOREAPP
        Assert.That(duration, Is.EqualTo(TimeSpan.Zero));
#endif
    }

    [Test]
    public void TryGetEventPairDuration_calculates_duration_between_multiple_out_of_order_configured_events()
    {
        // arrange
        var now = DateTime.UtcNow;
        var startEvent1 = TestHelpers.CreateEventWrittenEventArgs(EventIdStart, now, payload: 1L);
        var endEvent1 = TestHelpers.CreateEventWrittenEventArgs(EventIdEnd, now.AddMilliseconds(300), payload: 1L);
        var startEvent2 = TestHelpers.CreateEventWrittenEventArgs(EventIdStart, now, payload: 2L);
        var endEvent2 = TestHelpers.CreateEventWrittenEventArgs(EventIdEnd, now.AddMilliseconds(200), payload: 2L);
        var startEvent3 = TestHelpers.CreateEventWrittenEventArgs(EventIdStart, now, payload: 3L);
        var endEvent3 = TestHelpers.CreateEventWrittenEventArgs(EventIdEnd, now.AddMilliseconds(100), payload: 3L);

        _eventPairTimer.TryGetDuration(startEvent1, out _);
        _eventPairTimer.TryGetDuration(startEvent2, out _);
        _eventPairTimer.TryGetDuration(startEvent3, out _);

        // act
        Assert.That(_eventPairTimer.TryGetDuration(endEvent3, out var event3Duration), Is.EqualTo(DurationResult.FinalWithDuration));
        Assert.That(_eventPairTimer.TryGetDuration(endEvent2, out var event2Duration), Is.EqualTo(DurationResult.FinalWithDuration));
        Assert.That(_eventPairTimer.TryGetDuration(endEvent1, out var event1Duration), Is.EqualTo(DurationResult.FinalWithDuration));
#if NETCOREAPP
        Assert.That(event1Duration.TotalMilliseconds, Is.EqualTo(300));
        Assert.That(event2Duration.TotalMilliseconds, Is.EqualTo(200));
        Assert.That(event3Duration.TotalMilliseconds, Is.EqualTo(100));
#endif
    }
}

public class EventPairTimerBaseClass
{
    protected const int EventIdStart = 1, EventIdEnd = 2;
}
