namespace System.Diagnostics.Runtime.Util;

/// <summary>
/// Generating tags often involves heavy use of String.Format, which takes CPU time and needlessly re-allocates
/// strings. Pre-generating these labels helps keep resource use to a minimum.
/// </summary>
internal static class LabelGenerator
{
    internal static Dictionary<TEnum, string> MapEnumToLabelValues<TEnum>()
        where TEnum : Enum =>
        Enum.GetValues(typeof(TEnum)).Cast<TEnum>()
            .ToDictionary(k => k, v => (Enum.GetName(typeof(TEnum), v) ?? v.ToString()).ToSnakeCase());

    internal static string ToLabel(this bool b)
    {
        const string LabelValueTrue = "true", LabelValueFalse = "false";
        return b ? LabelValueTrue : LabelValueFalse;
    }
}
