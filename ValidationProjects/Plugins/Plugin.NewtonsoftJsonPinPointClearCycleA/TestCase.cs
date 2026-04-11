using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Plugin.NewtonsoftJsonPinPointClearCycleA;

public class LocalTypeA
{
    public int X { get; set; }
}

public class TestCase
{
    public void Execute()
    {
        JsonConvert.SerializeObject(new LocalTypeA { X = 42 });
        ClearCache();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ClearCache()
    {
        const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var callingAssembly = Assembly.GetCallingAssembly();
        var runtime = typeof(TypeDescriptor).Assembly;
        var reflectProviderType = runtime.GetType("System.ComponentModel.ReflectTypeDescriptionProvider")!;
        var nodeType = runtime.GetType("System.ComponentModel.TypeDescriptor+TypeDescriptionNode")!;

        TypeDescriptor.Refresh(callingAssembly);
        
        Prune(StaticField<IDictionary>(typeof(TypeDescriptor), "_defaultProviderInitialized", "s_defaultProviderInitialized"));

        foreach (var entry in Snapshot(StaticField<IDictionary>(typeof(TypeDescriptor), "_providerTable", "s_providerTable")))
            for (var node = entry.Value; node != null && nodeType.IsInstanceOfType(node); node = InstanceField(node, "Next"))
                if (InstanceField(node, "Provider") is var provider && reflectProviderType.IsInstanceOfType(provider))
                    Prune((IDictionary)(InstanceField(provider, "_typeData") ?? throw new InvalidOperationException("_typeData was null")));
        
        Prune(StaticField<IDictionary>(reflectProviderType, "s_attributeCache"));

        return;

        void Prune(IDictionary dict)
        {
            var snap = Snapshot(dict);
            foreach (var t in snap)
                if (Matches(t.Key) || Matches(t.Value))
                    dict.Remove(t.Key);
        }

        bool Matches(object? value)
        {
            if (value == null) return false;
            if (ReferenceEquals(value, callingAssembly)) return true;
            switch (value)
            {
                case Type type: return ReferenceEquals(type.Assembly, callingAssembly);
                case Assembly assembly: return ReferenceEquals(assembly, callingAssembly);
                case MemberInfo member: return ReferenceEquals(member.Module.Assembly, callingAssembly);
                case WeakReference weakRef: return Matches(weakRef.Target);
            }

            foreach (var name in new[] { "Target", "target", "_target" })
            {
                try
                {
                    if (value.GetType().GetProperty(name, instanceFlags) is { } prop &&
                        prop.GetIndexParameters().Length == 0 && prop.GetValue(value) is var propTarget &&
                        !ReferenceEquals(propTarget, value) && Matches(propTarget)) return true;
                    if (value.GetType().GetField(name, instanceFlags)?.GetValue(value) is var fieldTarget &&
                        !ReferenceEquals(fieldTarget, value) && Matches(fieldTarget)) return true;
                }
                catch
                {
                }
            }

            return ReferenceEquals(value.GetType().Assembly, callingAssembly);
        }

        static DictionaryEntry[] Snapshot(IDictionary dict)
        {
            var entries = new DictionaryEntry[dict.Count];
            dict.CopyTo(entries, 0);
            return entries;
        }

        static T StaticField<T>(Type type, params string[] names)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                       BindingFlags.FlattenHierarchy;
            foreach (var name in names)
            {
                var field = type.GetField(name, flags);
                if (field == null) continue;
                return (T)(field.GetValue(null) ?? throw new InvalidOperationException($"{name} was null"));
            }

            throw new MissingFieldException(type.FullName, string.Join("/", names));
        }

        static object? InstanceField(object instance, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            return (instance.GetType().GetField(name, flags) ?? throw new MissingFieldException(instance.GetType().FullName, name)).GetValue(instance);
        }
    }
}
