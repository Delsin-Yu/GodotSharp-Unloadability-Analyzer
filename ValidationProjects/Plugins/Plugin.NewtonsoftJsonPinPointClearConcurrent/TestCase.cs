using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Plugin.NewtonsoftJsonPinPointClearConcurrent;

public class LocalType
{
    public int Value { get; set; }
}

public class TestCase
{
    public void Execute()
    {
        var exceptions = new ConcurrentBag<Exception>();
        using var cts = new CancellationTokenSource();

        // Spawn 4 background threads that continuously serialize BCL types
        var threads = new Thread[4];
        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        JsonConvert.SerializeObject(DateTime.Now);
                        JsonConvert.SerializeObject(new List<int> { 1, 2, 3 });
                        JsonConvert.SerializeObject(new Dictionary<string, int> { { "x", 1 } });
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })
            { IsBackground = true };
            threads[i].Start();
        }

        // Main thread: serialize LocalType, then clear cache
        JsonConvert.SerializeObject(new LocalType { Value = 42 });
        ClearCache();

        // Signal cancellation and wait for threads
        cts.Cancel();
        foreach (var t in threads)
            t.Join(TimeSpan.FromSeconds(5));

        // Report any captured exceptions
        if (!exceptions.IsEmpty)
            throw new AggregateException("Background threads encountered exceptions during concurrent cleanup", exceptions);

        // Post-cleanup verification: BCL types should still serialize correctly
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        var json = JsonConvert.SerializeObject(dict);
        var roundTrip = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
        if (roundTrip == null || roundTrip["a"] != 1 || roundTrip["b"] != 2)
            throw new InvalidOperationException("Post-cleanup Newtonsoft BCL serialization failed for Dictionary<string,int>");
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
