# PW.Diagnostics.Runtime
A plugin for the [System.Diagnostics.DiagnosticSource](https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource) package, [exposing .NET runtime metrics](metrics-exposed.md) including:
- Garbage collection collection frequencies and timings by generation/ type, pause timings and GC CPU consumption ratio
- Heap size by generation
- Bytes allocated by small/ large object heap
- JIT compilations and JIT CPU consumption ratio
- Thread pool size, scheduling delays and reasons for growing/ shrinking
- Lock contention
- Exceptions thrown, broken down by type

These metrics are essential for understanding the performance of any non-trivial application. Even if your application is well instrumented, you're only getting half the story- what the runtime is doing completes the picture.

## Using this package
### Requirements
- .NET core 3.1 (runtime version 3.1.11+ is recommended)/ .NET 6.0/ .NET Framework 4.7.1
- The [PW.Diagnostics.Runtime](https://github.com/pengweiqhca/System.Diagnostics.Runtime) package

### Install it
The package can be installed from [nuget](https://www.nuget.org/packages/PW.Diagnostics.Runtime):
```powershell
dotnet add package PW.Diagnostics.Runtime
```

### Integration with Opentelemetry
See [example](https://github.com/pengweiqhca/System.Diagnostics.Runtime/blob/main/AspNetCoreExample/Program.cs#L17) and [extension method](https://github.com/pengweiqhca/System.Diagnostics.Runtime/blob/main/AspNetCoreExample/MeterProviderBuilderExtensions.cs)
