using System.Diagnostics.Runtime.EventListening;
using System.Diagnostics.Tracing;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.EventListening.Parsers;

[TestFixture]
public abstract class EventListenerIntegrationTestBase<TEventListener>
    where TEventListener : IEventListener
{
    private DotNetEventListener _eventListener = default!;
    protected TEventListener Parser { get; private set; } = default!;

    [SetUp]
    public void SetUp()
    {
        Parser = CreateListener();
        _eventListener = new DotNetEventListener(Parser, EventLevel.LogAlways, new());

        var now = DateTime.Now;

        // wait for event listener thread to spin up
        while (!_eventListener.StartedReceivingEvents)
        {
            Thread.Sleep(10);

            if (DateTime.Now.Subtract(now).TotalSeconds > 10) throw new TimeoutException();
        }
    }

    [TearDown]
    public void TearDown() => _eventListener.Dispose();

    protected abstract TEventListener CreateListener();
}
