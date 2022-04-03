using System.Diagnostics.Runtime.EventListening.Parsers;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.EventListening.Parsers;

[TestFixture]
public class ThreadPoolSchedulingParserTest : EventListenerIntegrationTestBase<ThreadPoolSchedulingParser>
{
    [Test]
    public void TestEvent()
    {
        var countdown = new CountdownEvent(10);

        Parser.Dequeue += () => countdown.Signal();

        for (var index = 0; index < 10; index++)
            ThreadPool.QueueUserWorkItem(_ => Thread.Sleep(10));

        Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(20)));
    }

    protected override ThreadPoolSchedulingParser CreateListener() => new();
}
