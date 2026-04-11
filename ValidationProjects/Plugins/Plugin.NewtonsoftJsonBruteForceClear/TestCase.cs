using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Plugin.NewtonsoftJsonBruteForceClear;

public class LocalType
{
    public int Value { get; set; }
}

public class TestCase
{
    public void Execute()
    {
        JsonConvert.SerializeObject(new LocalType { Value = 42 });
        Console.WriteLine("Serialized LocalType with Newtonsoft.Json.");

        var clearedEntries = ClearCache();
        Console.WriteLine($"Brute-force cleanup touched {clearedEntries} cache slot(s).");

        // Post-cleanup verification: BCL types should still serialize/deserialize correctly
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        var json = JsonConvert.SerializeObject(dict);
        var roundTrip = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
        if (roundTrip == null || roundTrip["a"] != 1 || roundTrip["b"] != 2)
            throw new InvalidOperationException("Post-cleanup Newtonsoft BCL serialization failed for Dictionary<string,int>");

        // Complex post-cleanup verification
        var complex = new List<Dictionary<string, object>>
        {
            new() { { "name", "test" }, { "value", 42 } },
            new() { { "name", "other" }, { "value", 99 } }
        };
        var complexJson = JsonConvert.SerializeObject(complex);
        var complexRoundTrip = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(complexJson);
        if (complexRoundTrip == null || complexRoundTrip.Count != 2)
            throw new InvalidOperationException("Post-cleanup Newtonsoft BCL serialization failed for complex nested type");
    }

    private static void ClearCache2()
    {
        return;

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "s_providerTable")]
        static extern ref object ProviderTableAccessor(TypeDescriptor descriptor);
    }

    static int ClearCache()
    {
        var assembly = typeof(TypeDescriptor).Assembly;
        var clearedEntries = 0;
        var rootPrefixes = new[] { "System.ComponentModel.ReflectTypeDescriptionProvider" };
        var rootNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.ComponentModel.TypeDescriptor::s_providerTable",
            "System.ComponentModel.TypeDescriptor::s_defaultProviderInitialized"
        };

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>();
        foreach (var type in assembly.GetTypes())
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.IsLiteral) continue;

            object? value;
            try
            {
                value = field.GetValue(null);
            }
            catch
            {
                continue;
            }

            if (value == null) continue;
            var rootName = $"{type.FullName}::{field.Name}";
            if (!rootNames.Contains(rootName) &&
                !rootPrefixes.Any(prefix => rootName.StartsWith(prefix, StringComparison.Ordinal))) continue;
            pending.Enqueue(value);
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current)) continue;

            clearedEntries += ClearThreadSafeStore(current);
            clearedEntries += ClearDictionaryLike(current);
            clearedEntries += ClearListLike(current);
            clearedEntries += ClearArrayLike(current);

            foreach (var child in EnumerateChildObjects(current, assembly))
            {
                pending.Enqueue(child);
            }
        }

        return clearedEntries;

        static IEnumerable<object> EnumerateChildObjects(object instance, Assembly referencAssembly)
        {
            var instanceType = instance.GetType();
            if (instanceType.Assembly != referencAssembly)
            {
                var namespaceName = instanceType.Namespace ?? string.Empty;
                if (!namespaceName.StartsWith("System.ComponentModel", StringComparison.Ordinal))
                {
                    yield break;
                }
            }

            foreach (var field in instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                         BindingFlags.Instance))
            {
                if (field.FieldType.IsPointer) continue;

                object? value;
                try
                {
                    value = field.GetValue(instance);
                }
                catch
                {
                    continue;
                }

                if (value == null) continue;
                yield return value;
            }
        }

        static int ClearListLike(object? instance)
        {
            if (instance is null or string) return 0;

            try
            {
                if (instance is IList list)
                {
                    var itemCount = list.Count;
                    list.Clear();
                    return itemCount;
                }

                var clearMethod = instance.GetType()
                    .GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                if (clearMethod == null) return 0;

                var countBeforeClear = TryGetCount(instance);
                clearMethod.Invoke(instance, null);
                return countBeforeClear;
            }
            catch
            {
                return 0;
            }
        }

        static int ClearArrayLike(object? instance)
        {
            if (instance is not Array array || array.Length == 0) return 0;
            if (array.GetType().GetElementType()?.IsValueType == true) return 0;
            Array.Clear(array, 0, array.Length);
            return array.Length;
        }

        static int ClearThreadSafeStore(object? store)
        {
            if (store == null) return 0;
            var storeType = store.GetType();
            var concurrentStoreField =
                storeType.GetField("_concurrentStore", BindingFlags.NonPublic | BindingFlags.Instance);
            if (concurrentStoreField?.GetValue(store) is var concurrentStore)
                return ClearDictionaryLike(concurrentStore);
            var storeField = storeType.GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance);
            if (storeField?.GetValue(store) is var dictionaryStore) return ClearDictionaryLike(dictionaryStore);
            return 0;
        }

        static int ClearDictionaryLike(object? dictionary)
        {
            if (dictionary is null or string) return 0;
            var count = TryGetCount(dictionary);

            try
            {
                if (dictionary is IDictionary nonGenericDictionary)
                {
                    nonGenericDictionary.Clear();
                    return count;
                }

                var clearMethod = dictionary.GetType()
                    .GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                if (clearMethod == null)
                {
                    return 0;
                }

                clearMethod.Invoke(dictionary, null);
                return count;
            }
            catch
            {
                return 0;
            }
        }

        static int TryGetCount(object value)
        {
            var countProperty = value.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            if (countProperty == null) return 0;
            try
            {
                return countProperty.GetValue(value) as int? ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}