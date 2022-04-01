using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using NUnit.Framework;

namespace System.Diagnostics.Runtime.Tests.Util;

[TestFixture]
public class LabelGeneratorTests
{
    [Test]
    public void MapEnumToLabelValues_will_generate_labels_with_snake_cased_names()
    {
        var labels = LabelGenerator.MapEnumToLabelValues<NativeRuntimeEventSource.GCReason>();

        Assert.That(labels[NativeRuntimeEventSource.GCReason.AllocLarge], Is.EqualTo("alloc_large"));
        Assert.That(labels[NativeRuntimeEventSource.GCReason.OutOfSpaceLOH], Is.EqualTo("out_of_space_loh"));
    }
}
