using System.Diagnostics.Metrics;
using System.Net;
using System.Text;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

public readonly record struct MeasurementValue(Instrument Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags, object? State)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(Instrument.Name).Append('=').Append(Value);

        if (Tags.Count > 0)
        {
            builder.Append(' ');

            foreach (var kv in Tags)
                builder.Append(kv.Key).Append('=').Append(WebUtility.UrlEncode(kv.Value?.ToString())).Append('&');
        }

        builder.Length -= 1;

        if (State != null) builder.Append(' ').Append(State);

        return true;
    }
}

public static class MeasurementValueExtensions
{
    public static double LastValue(this IReadOnlyCollection<MeasurementValue> measurements, string? instrument, string tagKey, object? tagValue) =>
        measurements.LastValue(instrument, new KeyValuePair<string, object?>(tagKey, tagValue));

    public static double LastValue(this IReadOnlyCollection<MeasurementValue> measurements, string? instrument, params KeyValuePair<string, object?>[] tags) =>
        Filter(measurements, instrument, tags).LastOrDefault().Value;

    public static double Sum(this IReadOnlyCollection<MeasurementValue> measurements, string? instrument, string tagKey, object? tagValue) =>
        measurements.Sum(instrument, new KeyValuePair<string, object?>(tagKey, tagValue));

    public static double Sum(this IReadOnlyCollection<MeasurementValue> measurements, string? instrument, params KeyValuePair<string, object?>[] tags) =>
        Filter(measurements, instrument, tags).Sum(x => x.Value);

    public static IReadOnlyCollection<double> Values(this IReadOnlyCollection<MeasurementValue> measurements, string? instrument, params KeyValuePair<string, object?>[] tags) =>
        Filter(measurements, instrument, tags).Select(x => x.Value).ToArray();

    private static IEnumerable<MeasurementValue> Filter(this IReadOnlyCollection<MeasurementValue> measurements, string? instrument, params KeyValuePair<string, object?>[] tags)
    {
        lock (measurements)
            return tags.Aggregate(measurements.Where(x => x.Instrument.Name == instrument),
                    (current, tag) => current.Where(x => x.Tags.Any(t => Compare(t, tag))))
                .ToArray();
    }

    private static bool Compare(KeyValuePair<string, object?> kv1, KeyValuePair<string, object?> kv2) =>
        kv1.Key == kv2.Key && (kv1.Value?.Equals(kv2.Value) ?? kv2.Value == null);
}
