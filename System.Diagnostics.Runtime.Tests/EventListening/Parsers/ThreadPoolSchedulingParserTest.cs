using System.Diagnostics.Runtime.EventListening.Parsers;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.EventListening.Parsers;

[TestFixture]
public class ThreadPoolSchedulingParserTest : EventListenerIntegrationTestBase<ThreadPoolSchedulingParser>
{
    [Test]
    public void TestEvent()
    {
        var countdown1 = new CountdownEvent(10);
        var countdown2 = new CountdownEvent(10);

        Parser.Enqueue += () => countdown1.Signal();
        Parser.Dequeue += () => countdown2.Signal();

        for (var index = 0; index < 10; index++)
            ThreadPool.QueueUserWorkItem(_ => Thread.Sleep(10));

        Assert.That(countdown1.CurrentCount, Is.InRange(0, 5));
        Assert.IsTrue(countdown2.Wait(TimeSpan.FromSeconds(2)));
    }

    protected override ThreadPoolSchedulingParser CreateListener() => new();
}
