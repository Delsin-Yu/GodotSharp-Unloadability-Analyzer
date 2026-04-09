using System.Text.Json;

namespace Plugin.SystemTextJson;

public class LocalType { public int Value { get; set; } }

public class TestCase
{
    public void Execute()
    {
        JsonSerializer.Serialize(new LocalType { Value = 42 });
    }
}
