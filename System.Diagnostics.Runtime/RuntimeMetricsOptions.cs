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
    /// Gets or sets a value indicating whether enable debugging metrics.
    /// </summary>
    public bool EnabledDebuggingMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether assembly metrics should be collected.
    /// </summary>
    public bool? AssembliesEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether contention metrics should be collected.
    /// </summary>
    public bool? ContentionEnabled { get; set; }
#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether dns metrics should be collected.
        /// </summary>
        public bool? DnsEnabled { get; set; }
#endif
    /// <summary>
    /// Gets or sets a value indicating whether exception metrics should be collected.
    /// </summary>
    public bool? ExceptionsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether garbage collection metrics should be collected.
    /// </summary>
    public bool? GcEnabled { get; set; }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether jitter metrics should be collected.
        /// </summary>
        public bool? JitEnabled { get; set; }
#endif

    /// <summary>
    /// Gets or sets a value indicating whether process metrics should be collected.
    /// </summary>
    public bool? ProcessEnabled { get; set; }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether sockets metrics should be collected.
        /// </summary>
        public bool? SocketsEnabled { get; set; }
#endif
    /// <summary>
    /// Gets or sets a value indicating whether threading metrics should be collected.
    /// </summary>
    public bool? ThreadingEnabled { get; set; }

    private bool IsAllEnabled => AssembliesEnabled == null
                                 && ContentionEnabled == null
#if NET6_0_OR_GREATER
                                     && DnsEnabled == null
#endif
                                 && ExceptionsEnabled == null
                                 && GcEnabled == null
#if NET6_0_OR_GREATER
                                     && JitEnabled == null
#endif
                                 && ProcessEnabled == null
#if NET6_0_OR_GREATER
                                     && SocketsEnabled == null
#endif
                                 && ThreadingEnabled == null;

    /// <summary>
    /// Gets a value indicating whether assembly metrics is enabled.
    /// </summary>
    internal bool IsAssembliesEnabled => AssembliesEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets a value indicating whether contention metrics is enabled.
    /// </summary>
    internal bool IsContentionEnabled => ContentionEnabled == true || IsAllEnabled;

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets a value indicating whether dns metrics is enabled.
        /// </summary>
        internal bool IsDnsEnabled => DnsEnabled == true || IsAllEnabled;
#endif
    /// <summary>
    /// Gets a value indicating whether exception metrics is enabled.
    /// </summary>
    internal bool IsExceptionsEnabled => ExceptionsEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets a value indicating whether garbage collection metrics is enabled.
    /// </summary>
    internal bool IsGcEnabled => GcEnabled == true || IsAllEnabled;

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets a value indicating whether jitter metrics is enabled.
        /// </summary>
        internal bool IsJitEnabled => JitEnabled == true || IsAllEnabled;
#endif

    /// <summary>
    /// Gets a value indicating whether process metrics is enabled.
    /// </summary>
    internal bool IsProcessEnabled => ProcessEnabled == true || IsAllEnabled;

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets a value indicating whether sockets metrics is enabled.
        /// </summary>
        internal bool IsSocketsEnabled =>
            SocketsEnabled == true || IsAllEnabled;
#endif

    /// <summary>
    /// Gets a value indicating whether threading metrics is enabled.
    /// </summary>
    internal bool IsThreadingEnabled => ThreadingEnabled == true || IsAllEnabled;

    /// <summary>
    /// Gets or sets a value indicating whether enable listen native event source.
    /// </summary>
    /// <remarks>If true, metrics will include more detail info.</remarks>
    public bool EnabledNativeRuntime { get; set; }
#if NETCOREAPP
        /// <summary>
        /// Gets or sets a value indicating whether enable listen system runtime event source.
        /// </summary>
        /// <remarks>If true, metrics will include more detail info.</remarks>
        public bool EnabledSystemRuntime { get; set; }
#else
    /// <summary>
    /// Gets or sets the name of the session to open. Should be unique across the machine.
    /// </summary>
    /// <remarks>If EnabledNativeRuntime is true and EtwSessionName has value, will start ETW session read metrics. Administrator rights required to start ETW.</remarks>
    public string? EtwSessionName { get; set; }
#endif
}
