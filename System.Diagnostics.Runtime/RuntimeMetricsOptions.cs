namespace System.Diagnostics.Runtime;

/// <summary>
/// Options to define the runtime metrics.
/// </summary>
public class RuntimeMetricsOptions
{
    /// <summary>
    /// Gets or sets a value indicating the metrics prefix to use.
    /// </summary>
    public string MetricPrefix { get; set; } = "process.runtime.dotnet.";

    /// <summary>
    /// Gets or sets a value indicating whether assembly metrics should be collected.
    /// </summary>
    public bool? AssembliesEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether contention metrics should be collected.
    /// </summary>
    public bool? ContentionEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exception metrics should be collected.
    /// </summary>
    public bool? ExceptionsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether garbage collection metrics should be collected.
    /// </summary>
    public bool? GcEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether process metrics should be collected.
    /// </summary>
    public bool? ProcessEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether threading metrics should be collected.
    /// </summary>
    public bool? ThreadingEnabled { get; set; }

    private bool IsAllEnabled => AssembliesEnabled == null
        && ContentionEnabled == null
        && ExceptionsEnabled == null
        && GcEnabled == null
        && ProcessEnabled == null
        && ThreadingEnabled == null;

    /// <summary>
    /// Gets a value indicating whether contention metrics is enabled.
    /// </summary>
    internal bool IsContentionEnabled => ContentionEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets a value indicating whether exception metrics is enabled.
    /// </summary>
    internal bool IsExceptionsEnabled => ExceptionsEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets a value indicating whether garbage collection metrics is enabled.
    /// </summary>
    internal bool IsGcEnabled => GcEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets a value indicating whether process metrics is enabled.
    /// </summary>
    internal bool IsProcessEnabled => ProcessEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets a value indicating whether threading metrics is enabled.
    /// </summary>
    internal bool IsThreadingEnabled => ThreadingEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets or sets a value indicating whether enable listen native event source.
    /// </summary>
    /// <remarks>If true, metrics will include more detail info.</remarks>
    public bool EnabledNativeRuntime { get; set; }
#if NETFRAMEWORK
    /// <summary>
    /// Gets or sets the name of the session to open. Should be unique across the machine.
    /// </summary>
    /// <remarks>If EnabledNativeRuntime is true and EtwSessionName has value, will start ETW session read metrics. Administrator rights required to start ETW.</remarks>
    public string? EtwSessionName { get; set; }
#endif
}
