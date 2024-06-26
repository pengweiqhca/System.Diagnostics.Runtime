using OpenTelemetry.Metrics;

// ReSharper disable once CheckNamespace
namespace System.Diagnostics.Runtime;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables runtime instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">Runtime metrics options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddExampleInstrumentation(
        this MeterProviderBuilder builder,
        Action<RuntimeMetricsOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var options = new RuntimeMetricsOptions();

        if (configure == null)
        {
            options.EnabledNativeRuntime = true;
#if NETFRAMEWORK
            options.EtwSessionName = "NetFrameworkExample";
#endif
        }
        else configure.Invoke(options);

        return builder.AddProcessRuntimeInstrumentation(options);
    }
}
