using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Plugin.SystemTextJsonClearCacheConcurrent;

public class LocalType { public int Value { get; set; } }

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
                        JsonSerializer.Serialize(DateTime.Now);
                        JsonSerializer.Serialize(new List<int> { 1, 2, 3 });
                        JsonSerializer.Serialize(new Dictionary<string, int> { { "x", 1 } });
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
        JsonSerializer.Serialize(new LocalType { Value = 42 });
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
        var json = JsonSerializer.Serialize(dict);
        var roundTrip = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
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
