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
    }

    private void ClearCache()
    {
        var assembly = typeof(JsonSerializerOptions).Assembly;
        var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
        var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public);
        clearCacheMethod?.Invoke(null, new object[] { null! });
    }
}
