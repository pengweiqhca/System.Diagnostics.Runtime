namespace System.Diagnostics.Runtime.EventListening;

[AttributeUsage(AttributeTargets.Event)]
public class CounterNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
