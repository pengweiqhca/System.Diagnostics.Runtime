using NUnit.Framework;
using System.Diagnostics.Runtime.EventListening.Parsers;

namespace System.Diagnostics.Runtime.Tests.EventListening.Parsers;

[TestFixture]
public class ThreadPoolSchedulingParserTest : EventListenerIntegrationTestBase<FrameworkEventParser>
{
    [Test]
    public async Task TestEvent()
    {
        var countdown1 = new CountdownEvent(10);
        var countdown2 = new CountdownEvent(10);

        Parser.Enqueue += () => countdown1.Signal();
        Parser.Dequeue += () => countdown2.Signal();

        for (var index = 0; index < 10; index++)
            ThreadPool.QueueUserWorkItem(static _ => Thread.Sleep(10));

        await Task.Delay(1000).ConfigureAwait(false);

        Assert.That(countdown1.CurrentCount, Is.InRange(0, 5));
        Assert.IsTrue(countdown2.Wait(TimeSpan.FromSeconds(2)));
    }

    protected override FrameworkEventParser CreateListener() => new();
}
