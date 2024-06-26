using System.Diagnostics.Metrics;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

public static class InstrumentTest
{
    private static readonly IReadOnlyCollection<string> DebugMetrics = ["process.runtime.dotnet.debug.event.count", "process.runtime.dotnet.debug.event.seconds.total", "process.runtime.dotnet.debug.publish.threads.count"];

    public static Task<IReadOnlyCollection<MeasurementValue>> Assert(
        Action<IReadOnlyList<MeasurementValue>> assert, params string?[] instruments) =>
        Assert(static () => { }, assert, instruments);

    public static async Task<IReadOnlyCollection<MeasurementValue>> Assert(Action act,
        Action<IReadOnlyList<MeasurementValue>> assert, params string?[] instruments)
    {
        var tcs = new TaskCompletionSource<object?>();

        // arrange
        var measurements = new List<MeasurementValue>();

        var names = instruments.ToList();

        var task = ListenMetricsAsync(measurements, () =>
        {
            act();

            return tcs.Task;
        }, names);

        try
        {
            NUnit.Framework.Assert.IsEmpty(names);

            assert(measurements);
        }
        finally
        {
            tcs.TrySetResult(null);
        }

        return await task.ConfigureAwait(false);
    }

    public static async Task<IReadOnlyCollection<MeasurementValue>> Assert(Func<Task> act,
        Action<IReadOnlyList<MeasurementValue>> assert, params string?[] instruments)
    {
        var tcs1 = new TaskCompletionSource<object?>();
        var tcs2 = new TaskCompletionSource<object?>();

        // arrange
        var measurements = new List<MeasurementValue>();

        var names = instruments.ToList();

        var task = ListenMetricsAsync(measurements, async () =>
        {
            try
            {
                await act().ConfigureAwait(false);

                tcs1.TrySetResult(null);
            }
            catch(Exception ex)
            {
                tcs1.TrySetException(ex);
            }

            await tcs2.Task.ConfigureAwait(false);
        }, names);

        try
        {
            await tcs1.Task.ConfigureAwait(false);

            NUnit.Framework.Assert.IsEmpty(names);

            assert(measurements);
        }
        finally
        {
            tcs2.TrySetResult(null);
        }

        return await task.ConfigureAwait(false);
    }

    private static async Task<IReadOnlyCollection<MeasurementValue>> ListenMetricsAsync(ICollection<MeasurementValue> measurements,
        Func<Task> action, List<string?> names)
    {
        var debugMeasurements = new List<MeasurementValue>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (names.Contains(instrument.Name))
                {
                    names.Remove(instrument.Name);

                    meterListener.EnableMeasurementEvents(instrument);
                }
                else if (DebugMetrics.Contains(instrument.Name))
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
#if NETFRAMEWORK
        using var timer = new Timer(static state => ((MeterListener)state!).RecordObservableInstruments(), listener, 100, 100);
#else
        var timer = new Timer(static state => ((MeterListener)state!).RecordObservableInstruments(), listener, 100, 100);

        await using var _ = timer.ConfigureAwait(false);
#endif
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (DebugMetrics.Contains(instrument.Name))
                debugMeasurements.Add(new(instrument, measurement, tags.ToArray(), state));
            else
                lock (measurements)
                    measurements.Add(new(instrument, measurement, tags.ToArray(), state));
        });

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (DebugMetrics.Contains(instrument.Name))
                debugMeasurements.Add(new(instrument, measurement, tags.ToArray(), state));
            else
                lock (measurements)
                    measurements.Add(new(instrument, measurement, tags.ToArray(), state));
        });

        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (DebugMetrics.Contains(instrument.Name))
                debugMeasurements.Add(new(instrument, measurement, tags.ToArray(), state));
            else
                lock (measurements)
                    measurements.Add(new(instrument, measurement, tags.ToArray(), state));
        });

        listener.MeasurementsCompleted = (instrument, _) => listener.DisableMeasurementEvents(instrument);

        listener.Start();

        await action().ConfigureAwait(false);

        return debugMeasurements;
    }
}
