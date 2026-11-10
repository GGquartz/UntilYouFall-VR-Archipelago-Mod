using System.Reflection;
using HarmonyLib;

namespace UntilYouFallArchipelago;

internal static class HarmonyExtensions
{
    public static void Hook<THandler>(
        this HarmonyLib.Harmony harmony,
        MethodBase? target,
        string? prefix = null,
        string? postfix = null,
        string? finalizer = null,
        int? priority = null
    )
    {
        if (target == null)
            throw new InvalidOperationException(
                $"Harmony target not found for {typeof(THandler).Name} (prefix={prefix} postfix={postfix} finalizer={finalizer})"
            );

        harmony.Patch(
            target,
            prefix: Handler<THandler>(prefix, priority),
            postfix: Handler<THandler>(postfix, priority),
            finalizer: Handler<THandler>(finalizer, priority)
        );
    }

    private static HarmonyMethod? Handler<THandler>(string? name, int? priority)
    {
        if (name == null)
            return null;

        var handler = new HarmonyMethod(typeof(THandler), name);
        if (priority != null)
            handler.priority = priority.Value;

        return handler;
    }
}
