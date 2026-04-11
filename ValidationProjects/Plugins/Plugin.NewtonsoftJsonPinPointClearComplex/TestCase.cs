using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Plugin.NewtonsoftJsonPinPointClearComplex;

public class BaseModel
{
    public string Name { get; set; } = "";
}

public class DerivedModel : BaseModel
{
    public int Score { get; set; }
}

public class NestedModel
{
    public BaseModel? Inner { get; set; }
    public List<int> Values { get; set; } = new();
}

public class NestedModelConverter : JsonConverter<NestedModel>
{
    public override void WriteJson(JsonWriter writer, NestedModel? value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("wrapped");
        writer.WriteStartObject();
        writer.WritePropertyName("Inner");
        serializer.Serialize(writer, value?.Inner);
        writer.WritePropertyName("Values");
        serializer.Serialize(writer, value?.Values);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    public override NestedModel? ReadJson(JsonReader reader, Type objectType, NestedModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = Newtonsoft.Json.Linq.JObject.Load(reader);
        var wrapped = obj["wrapped"]!;
        return new NestedModel
        {
            Inner = wrapped["Inner"]?.ToObject<BaseModel>(serializer),
            Values = wrapped["Values"]?.ToObject<List<int>>(serializer) ?? new List<int>()
        };
    }
}

public class TestCase
{
    public void Execute()
    {
        // 1. Polymorphic serialization with TypeNameHandling.Auto
        var list = new List<BaseModel>
        {
            new BaseModel { Name = "base" },
            new DerivedModel { Name = "derived", Score = 99 }
        };

        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
        var json = JsonConvert.SerializeObject(list, settings);
        var deserialized = JsonConvert.DeserializeObject<List<BaseModel>>(json, settings);

        if (deserialized == null || deserialized.Count != 2)
            throw new InvalidOperationException("Polymorphic deserialization failed: wrong count");
        if (deserialized[0] is not BaseModel || deserialized[0] is DerivedModel)
            throw new InvalidOperationException("Polymorphic deserialization failed: first item is not BaseModel");
        if (deserialized[1] is not DerivedModel dm || dm.Score != 99)
            throw new InvalidOperationException("Polymorphic deserialization failed: second item is not DerivedModel with Score=99");

        // 2. Custom converter for NestedModel
        var nested = new NestedModel
        {
            Inner = new DerivedModel { Name = "inner", Score = 42 },
            Values = new List<int> { 1, 2, 3 }
        };
        var converterSettings = new JsonSerializerSettings { Converters = { new NestedModelConverter() } };
        var nestedJson = JsonConvert.SerializeObject(nested, converterSettings);
        if (!nestedJson.Contains("wrapped"))
            throw new InvalidOperationException("Custom converter did not produce 'wrapped' envelope");
        var nestedBack = JsonConvert.DeserializeObject<NestedModel>(nestedJson, converterSettings);
        if (nestedBack?.Inner?.Name != "inner" || nestedBack.Values.Count != 3)
            throw new InvalidOperationException("Custom converter round-trip failed");

        // 3. Clear cache (pinpoint)
        ClearCache();

        // 4. Post-cleanup BCL verification: Dictionary<string,int>
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        var dictJson = JsonConvert.SerializeObject(dict);
        var dictRoundTrip = JsonConvert.DeserializeObject<Dictionary<string, int>>(dictJson);
        if (dictRoundTrip == null || dictRoundTrip["a"] != 1 || dictRoundTrip["b"] != 2)
            throw new InvalidOperationException("Post-cleanup Newtonsoft BCL serialization failed for Dictionary<string,int>");

        // 5. Post-cleanup BCL verification: DateTime
        var now = DateTime.UtcNow;
        var dtJson = JsonConvert.SerializeObject(now);
        var dtRoundTrip = JsonConvert.DeserializeObject<DateTime>(dtJson);
        if (Math.Abs((dtRoundTrip - now).TotalSeconds) > 1)
            throw new InvalidOperationException("Post-cleanup Newtonsoft BCL serialization failed for DateTime");
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
