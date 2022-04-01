using System.Linq.Expressions;

namespace System.Diagnostics.Runtime.Tests.IntegrationTests;

public static class RuntimeEventHelper
{
    public static void CompileMethods(Expression<Func<int>> toCompile, int times = 100)
    {
        for (var i = 0; i < times; i++)
        {
            toCompile.Compile();
        }
    }
}
