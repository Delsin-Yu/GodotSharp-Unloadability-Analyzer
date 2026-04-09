using Newtonsoft.Json;

namespace Plugin.NewtonsoftJson;

public class LocalType { public int Value { get; set; } }

public class TestCase
{
    public void Execute()
    {
        JsonConvert.SerializeObject(new LocalType { Value = 42 });
    }
}
