using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Plugin.TypeDescriptorCycleB;

public class LocalTypeB { }

public class LocalProviderB : TypeDescriptionProvider { }

public class TestCase
{
    public void Execute()
    {
        var provider = new LocalProviderB();
        TypeDescriptor.AddProvider(provider, typeof(LocalTypeB));

        TypeDescriptor.GetProperties(typeof(LocalTypeB));
        TypeDescriptor.GetConverter(typeof(LocalTypeB));

        TypeDescriptor.RemoveProvider(provider, typeof(LocalTypeB));

        ClearTypeDescriptorCaches();

        // Post-cleanup verification: BCL TypeDescriptor should still work
        var conv = TypeDescriptor.GetConverter(typeof(int));
        if (conv == null || conv.GetType().Name != "Int32Converter")
            throw new InvalidOperationException("BCL TypeDescriptor broken after CycleA cleanup");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ClearTypeDescriptorCaches()
    {
        const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var callingAssembly = Assembly.GetCallingAssembly();
        var runtime = typeof(TypeDescriptor).Assembly;
        var reflectProviderType = runtime.GetType("System.ComponentModel.ReflectTypeDescriptionProvider")!;
        var nodeType = runtime.GetType("System.ComponentModel.TypeDescriptor+TypeDescriptionNode")!;

        TypeDescriptor.Refresh(callingAssembly);

        Prune(StaticField<IDictionary>(typeof(TypeDescriptor), "_defaultProviderInitialized", "s_defaultProviderInitialized"));

        var providerTable = StaticField<IDictionary>(typeof(TypeDescriptor), "_providerTable", "s_providerTable");

        Prune(providerTable);

        foreach (var entry in Snapshot(providerTable))
            for (var node = entry.Value; node != null && nodeType.IsInstanceOfType(node); node = InstanceField(node, "Next"))
                if (InstanceField(node, "Provider") is var provider && reflectProviderType.IsInstanceOfType(provider))
                    Prune((IDictionary)(InstanceField(provider, "_typeData") ?? throw new InvalidOperationException("_typeData was null")));

        if (TryStaticField<IDictionary>(reflectProviderType, out var attributeCache, "s_attributeCache"))
            Prune(attributeCache);

        return;

        void Prune(IDictionary dict)
        {
            var snap = Snapshot(dict);
            foreach (var e in snap)
                if (Matches(e.Key) || Matches(e.Value))
                    dict.Remove(e.Key);
        }

        bool Matches(object? value)
        {
            if (value == null) return false;
            if (ReferenceEquals(value, callingAssembly)) return true;
            switch (value)
            {
                case Type t: return ReferenceEquals(t.Assembly, callingAssembly);
                case Assembly a: return ReferenceEquals(a, callingAssembly);
                case MemberInfo m: return ReferenceEquals(m.Module.Assembly, callingAssembly);
                case WeakReference w: return Matches(w.Target);
            }
            foreach (var name in new[] { "Target", "target", "_target" })
            {
                try { if (value.GetType().GetProperty(name, instanceFlags) is { } prop && prop.GetIndexParameters().Length == 0 && prop.GetValue(value) is { } target && !ReferenceEquals(target, value) && Matches(target)) return true; } catch { }
                try { if (value.GetType().GetField(name, instanceFlags)?.GetValue(value) is { } target && !ReferenceEquals(target, value) && Matches(target)) return true; } catch { }
            }
            return ReferenceEquals(value.GetType().Assembly, callingAssembly);
        }

        static DictionaryEntry[] Snapshot(IDictionary dict) { var a = new DictionaryEntry[dict.Count]; dict.CopyTo(a, 0); return a; }

        static T StaticField<T>(Type type, params string[] names)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            foreach (var name in names) { var field = type.GetField(name, flags); if (field != null) return (T)(field.GetValue(null) ?? throw new InvalidOperationException($"{name} was null")); }
            throw new MissingFieldException(type.FullName, string.Join("/", names));
        }

        static bool TryStaticField<T>(Type type, out T value, params string[] names)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            foreach (var name in names) { var field = type.GetField(name, flags); if (field != null && field.GetValue(null) is T v) { value = v; return true; } }
            value = default!;
            return false;
        }

        static object? InstanceField(object instance, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            return (instance.GetType().GetField(name, flags) ?? throw new MissingFieldException(instance.GetType().FullName, name)).GetValue(instance);
        }
    }
}
