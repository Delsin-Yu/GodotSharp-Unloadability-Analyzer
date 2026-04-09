using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace PluginHost;

internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: PluginHost <path-to-plugin.dll>");
            return 2;
        }

        string pluginPath = Path.GetFullPath(args[0]);
        string pluginName = Path.GetFileNameWithoutExtension(pluginPath);

        var weakRef = LoadAndExecute(pluginPath);

        for (int i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        if (weakRef.IsAlive)
        {
            Console.WriteLine($"FAIL (Leaked): {pluginName}");
            return 1;
        }
        else
        {
            Console.WriteLine($"PASS (Unloaded): {pluginName}");
            return 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference LoadAndExecute(string pluginPath)
    {
        var alc = new PluginLoadContext(pluginPath);
        var assembly = alc.LoadFromAssemblyPath(pluginPath);

        // Find first type with a public parameterless Execute() method
        Type? targetType = null;
        MethodInfo? executeMethod = null;
        foreach (var type in assembly.GetExportedTypes())
        {
            var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (method != null)
            {
                targetType = type;
                executeMethod = method;
                break;
            }
        }

        if (targetType == null || executeMethod == null)
        {
            throw new InvalidOperationException(
                $"No type with a public parameterless Execute() method found in {pluginPath}");
        }

        var instance = Activator.CreateInstance(targetType)!;
        try
        {
            executeMethod.Invoke(instance, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION during Execute(): {ex.InnerException?.Message ?? ex.Message}");
        }

        var weakRef = new WeakReference(alc, trackResurrection: true);
        alc.Unload();
        return weakRef;
    }
}
