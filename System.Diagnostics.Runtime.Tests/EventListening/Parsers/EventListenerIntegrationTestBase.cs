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

        _eventListener = new DotNetEventListener(Parser, EventLevel.LogAlways);
    }

    [TearDown]
    public void TearDown()
    {
        _eventListener.Dispose();

        Assert.True(_eventListener.StartedReceivingEvents);
    }

    protected abstract TEventListener CreateListener();
}
