using System.Diagnostics.Runtime.EventListening.Parsers;
using NUnit.Framework;

#if NET
namespace System.Diagnostics.Runtime.Tests.EventListening.Parsers;

[TestFixture]
public class SystemRuntimeParserTests : EventListenerIntegrationTestBase<SystemRuntimeEventParser>
{
    [Test]
    public void TestEvent()
    {
        var resetEvent = new AutoResetEvent(false);
        Parser.AllocRate += e =>
        {
            resetEvent.Set();
            Assert.That(e.IncrementedBy, Is.GreaterThan(0));
        };

        Assert.IsTrue(resetEvent.WaitOne(TimeSpan.FromSeconds(2)));
    }

    protected override SystemRuntimeEventParser CreateListener() => new();
}
#endif
