using OpenTelemetry.Metrics;

namespace System.Diagnostics.Runtime;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables process runtime instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddProcessRuntimeInstrumentation(this MeterProviderBuilder builder) =>
        AddProcessRuntimeInstrumentation(builder, null);

    /// <summary>
    /// Enables process runtime instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="options">Runtime metrics options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddProcessRuntimeInstrumentation(
        this MeterProviderBuilder builder,
        RuntimeMetricsOptions? options)
    {
        var instrumentation = new RuntimeInstrumentation(options ?? new());

        builder.AddMeter(RuntimeInstrumentation.InstrumentationName);

        return builder.AddInstrumentation(() => instrumentation).AddProcessInstrumentation()
            .AddRuntimeInstrumentation();
    }
}
