using System.Diagnostics.Runtime.EventListening.Parsers;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.EventListening.Parsers;

[TestFixture]
public class ThreadPoolSchedulingParserTest : EventListenerIntegrationTestBase<ThreadPoolSchedulingParser>
{
    [Test]
    public void TestEvent()
    {
        var resetEvent = new AutoResetEvent(false);
        Parser.Dequeue += e =>
        {
            resetEvent.Set();
            Assert.That(e.EnqueueDuration, Is.GreaterThan(TimeSpan.Zero));
        };

        for (var index = 0; index < 10; index++)
            ThreadPool.QueueUserWorkItem(_ => Thread.Sleep(1000));

        Assert.IsTrue(resetEvent.WaitOne(TimeSpan.FromSeconds(2)));
    }

    protected override ThreadPoolSchedulingParser CreateListener() => new();
}
