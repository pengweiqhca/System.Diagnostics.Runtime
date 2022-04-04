namespace System.Diagnostics.Runtime.EventListening;

public readonly record struct MeanCounterValue(int Count, double Mean)
{
    public double Total => Count * Mean;
}

public readonly record struct IncrementingCounterValue(double IncrementedBy);
