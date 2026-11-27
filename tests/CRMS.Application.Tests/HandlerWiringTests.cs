using System.Reflection;
using CRMS.Application.Common;
using Xunit;

namespace CRMS.Application.Tests;

/// <summary>
/// A handler that declares a service field but never assigns it compiles fine and then fails on every
/// call with a null reference, which ApplicationService swallows into a generic error. NAMP booking
/// broke this way: the deploy and retry handlers used the stage authoriser without taking it in their
/// constructors. Every handler here is built with stand-in services, and every service field must be set.
/// </summary>
public class HandlerWiringTests
{
    public class Stand : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static readonly MethodInfo CreateProxy = typeof(DispatchProxy)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(DispatchProxy.Create) && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2);

    private static object? StandIn(Type type)
    {
        if (!type.IsInterface) return null;
        try { return CreateProxy.MakeGenericMethod(type, typeof(Stand)).Invoke(null, null); }
        catch { return null; }
    }

    private static IEnumerable<Type> Handlers() => typeof(IRequestHandler<,>).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
        .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

    [Fact]
    public void EveryHandlerSetsEveryServiceItUses()
    {
        var unset = new List<string>();
        var built = 0;

        foreach (var type in Handlers())
        {
            var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor is null) continue;

            object handler;
            try { handler = ctor.Invoke(ctor.GetParameters().Select(p => StandIn(p.ParameterType)).ToArray()); }
            catch { continue; }
            built++;

            var suppliable = ctor.GetParameters()
                .Where(p => p.ParameterType.IsInterface && StandIn(p.ParameterType) is not null)
                .Select(p => p.ParameterType)
                .ToHashSet();

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (!field.IsInitOnly || !field.FieldType.IsInterface) continue;
                // A field whose type the constructor never receives cannot be set from it; one it does
                // receive but leaves null is the same fault in a different shape. Both are reported.
                if (field.GetValue(handler) is null && (suppliable.Contains(field.FieldType) || !ctor.GetParameters().Any(p => p.ParameterType == field.FieldType)))
                    unset.Add($"{type.Name}.{field.Name} ({field.FieldType.Name})");
            }
        }

        Assert.True(built > 100, $"Only {built} handlers could be built; the check is not covering them.");
        Assert.True(unset.Count == 0, "Handler services never set:\n" + string.Join("\n", unset));
    }
}
