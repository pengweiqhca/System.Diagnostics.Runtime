namespace System.Diagnostics.Runtime.EventListening;

[AttributeUsage(AttributeTargets.Event)]
public class CounterNameAttribute : Attribute
{
    public CounterNameAttribute(string name) => Name = name;

    public string Name { get; }
}
