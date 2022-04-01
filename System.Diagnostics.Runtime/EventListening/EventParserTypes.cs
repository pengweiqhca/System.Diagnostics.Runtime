using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace System.Diagnostics.Runtime.EventListening;

internal static class EventParserTypes
{
    private static readonly IReadOnlyCollection<Type> InterfaceTypesToIgnore = new[]
    {
        typeof(IEvents),
        typeof(IVerboseEvents),
        typeof(IInfoEvents),
        typeof(IWarningEvents),
        typeof(IErrorEvents),
        typeof(IAlwaysEvents),
        typeof(ICriticalEvents),
        typeof(ICounterEvents)
    };

    internal static IEnumerable<Type> GetEventInterfaces(Type t)
    {
        return t.GetInterfaces()
            .Where(i => typeof(IEvents).IsAssignableFrom(i) && !InterfaceTypesToIgnore.Contains(i));
    }

    internal static IEnumerable<Type> GetEventInterfaces(Type t, EventLevel atLevelAndBelow)
    {
        return GetEventInterfaces(t)
            .Where(type => GetEventLevel(type) <= atLevelAndBelow);
    }

    internal static IEnumerable<Type> GetEventInterfacesForCurrentRuntime(Type t, EventLevel atLevelAndBelow)
    {
        return GetEventInterfaces(t, atLevelAndBelow)
            .Where(AreEventsSupportedByRuntime);
    }

    internal static IReadOnlyCollection<EventLevel> GetLevelsFromParser(Type type)
    {
        return GetEventInterfaces(type)
            .Select(GetEventLevel)
            .OrderBy(x => x)
            .Distinct()
            .ToArray();
    }

    private static EventLevel GetEventLevel(Type t)
    {
        // Captures ICounterEvents too as it inherits from IAlwaysEvents
        if (typeof(IAlwaysEvents).IsAssignableFrom(t))
            return EventLevel.LogAlways;

        if (typeof(IVerboseEvents).IsAssignableFrom(t))
            return EventLevel.Verbose;

        if (typeof(IInfoEvents).IsAssignableFrom(t))
            return EventLevel.Informational;

        if (typeof(IWarningEvents).IsAssignableFrom(t))
            return EventLevel.Warning;

        if (typeof(IErrorEvents).IsAssignableFrom(t))
            return EventLevel.Error;

        if (typeof(ICriticalEvents).IsAssignableFrom(t))
            return EventLevel.Critical;

        throw new InvalidOperationException($"Unexpected type '{t}'");
    }

    internal static IEnumerable<Type> GetEventParsers()
    {
        return GetEventParsers(typeof(IEventListener).Assembly);
    }

    internal static IEnumerable<Type> GetEventParsers(Assembly fromAssembly)
    {
        return fromAssembly
            .GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && x.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventParser<>)));
    }

    internal static Lazy<Version?> CurrentRuntimeVersion = new(() =>
    {
        var split = RuntimeInformation.FrameworkDescription.Split(' ');
        if (split.Length < 2)
            return null;
#if NETCOREAPP
        var versionPart = split[^1];
#else
        var versionPart = split[split.Length - 1];
#endif
        // Handle preview version strings, e.g. .NET 6.0.0-preview.7.21377.19.
        var hyphenIndex = versionPart.IndexOf('-');
        if (hyphenIndex > -1)
#if NETCOREAPP
            versionPart = versionPart[..hyphenIndex];
#else
            versionPart = versionPart.Substring(0, hyphenIndex);
#endif
        return Version.TryParse(versionPart, out var version) ? new Version(version.Major, version.Minor) : null;
    });

    internal static bool AreEventsSupportedByRuntime(Type type)
    {
        var eventVer = GetVersionOfEvents(type);

        if (CurrentRuntimeVersion.Value == null)
            // Assume if this is being run, it's on .net core 3.1+
            return eventVer == LowestSupportedVersion;

        return eventVer <= CurrentRuntimeVersion.Value;
    }


    private static readonly Version LowestSupportedVersion = new(3, 1);
    private static readonly Regex VersionRegex = new("V(?<major>[0-9]+)_(?<minor>[0-9]+)", RegexOptions.Compiled);
    private static Version GetVersionOfEvents(Type type)
    {
        if (!typeof(IEvents).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type} does not implement {nameof(IEvents)}");

        var match = VersionRegex.Match(type.Name);

        if (match is not { Success: true })
            // Defaults to 3.0 (haven't converted all existed interfaces into type interfaces)
            return new Version(3, 0);

        return new Version(int.Parse(match.Groups["major"].Value), int.Parse(match.Groups["minor"].Value));
    }
}
