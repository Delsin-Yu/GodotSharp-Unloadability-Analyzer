using System.Reflection;
using System.Text.Json;

namespace Plugin.SystemTextJsonClearCache;

public class LocalType { public int Value { get; set; } }

public class TestCase
{
    public void Execute()
    {
        // Serialize a plugin-defined type — this normally caches Type refs and prevents unload.
        JsonSerializer.Serialize(new LocalType { Value = 42 });

        // Attempt to clear the cache via the internal JsonSerializerOptionsUpdateHandler.
        ClearCache();

        // Post-cleanup verification: BCL types should still serialize/deserialize correctly
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        var json = System.Text.Json.JsonSerializer.Serialize(dict);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        if (roundTrip == null || roundTrip["a"] != 1 || roundTrip["b"] != 2)
            throw new InvalidOperationException("Post-cleanup STJ BCL serialization failed for Dictionary<string,int>");
    }

    private void ClearCache()
    {
        var assembly = typeof(JsonSerializerOptions).Assembly;
        var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
        var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public);
        clearCacheMethod?.Invoke(null, new object[] { null! });
    }
}
