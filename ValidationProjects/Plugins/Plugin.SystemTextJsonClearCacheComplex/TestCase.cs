using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plugin.SystemTextJsonClearCacheComplex;

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
    public override NestedModel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var wrapped = doc.RootElement.GetProperty("wrapped");
        var inner = wrapped.TryGetProperty("Inner", out var innerEl)
            ? JsonSerializer.Deserialize<BaseModel>(innerEl.GetRawText(), options)
            : null;
        var values = wrapped.TryGetProperty("Values", out var valuesEl)
            ? JsonSerializer.Deserialize<List<int>>(valuesEl.GetRawText(), options)
            : new List<int>();
        return new NestedModel { Inner = inner, Values = values ?? new List<int>() };
    }

    public override void Write(Utf8JsonWriter writer, NestedModel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("wrapped");
        writer.WriteStartObject();
        writer.WritePropertyName("Inner");
        JsonSerializer.Serialize(writer, value.Inner, options);
        writer.WritePropertyName("Values");
        JsonSerializer.Serialize(writer, value.Values, options);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

public class TestCase
{
    public void Execute()
    {
        // 1. Serialize with custom converter for NestedModel
        var options = new JsonSerializerOptions
        {
            Converters = { new NestedModelConverter() }
        };

        var nested = new NestedModel
        {
            Inner = new DerivedModel { Name = "inner", Score = 42 },
            Values = new List<int> { 1, 2, 3 }
        };

        var json = JsonSerializer.Serialize(nested, options);
        if (!json.Contains("wrapped"))
            throw new InvalidOperationException("Custom converter did not produce 'wrapped' envelope");

        // 2. Deserialize back and verify
        var back = JsonSerializer.Deserialize<NestedModel>(json, options);
        if (back?.Inner?.Name != "inner" || back.Values.Count != 3)
            throw new InvalidOperationException("Custom converter round-trip failed");

        // Also serialize a DerivedModel directly to exercise type caching
        var derived = new DerivedModel { Name = "derived", Score = 99 };
        var derivedJson = JsonSerializer.Serialize(derived);
        var derivedBack = JsonSerializer.Deserialize<DerivedModel>(derivedJson);
        if (derivedBack?.Score != 99)
            throw new InvalidOperationException("DerivedModel round-trip failed");

        // 3. Clear cache
        ClearCache();

        // 4. Post-cleanup BCL verification: Dictionary<string,int>
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        var dictJson = JsonSerializer.Serialize(dict);
        var dictRoundTrip = JsonSerializer.Deserialize<Dictionary<string, int>>(dictJson);
        if (dictRoundTrip == null || dictRoundTrip["a"] != 1 || dictRoundTrip["b"] != 2)
            throw new InvalidOperationException("Post-cleanup STJ BCL serialization failed for Dictionary<string,int>");

        // 5. Post-cleanup BCL verification: List<int>
        var list = new List<int> { 10, 20, 30 };
        var listJson = JsonSerializer.Serialize(list);
        var listRoundTrip = JsonSerializer.Deserialize<List<int>>(listJson);
        if (listRoundTrip == null || listRoundTrip.Count != 3 || listRoundTrip[1] != 20)
            throw new InvalidOperationException("Post-cleanup STJ BCL serialization failed for List<int>");
    }

    private void ClearCache()
    {
        var assembly = typeof(JsonSerializerOptions).Assembly;
        var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
        var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public);
        clearCacheMethod?.Invoke(null, new object[] { null! });
    }
}
