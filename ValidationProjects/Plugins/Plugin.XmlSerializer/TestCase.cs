using System.Xml.Serialization;

namespace Plugin.XmlSerializer;

public class LocalType { public int Value { get; set; } }

public class TestCase
{
    public void Execute()
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LocalType));
        using var writer = new System.IO.StringWriter();
        serializer.Serialize(writer, new LocalType { Value = 42 });
        var xml = writer.ToString();
        System.Console.WriteLine($"Serialized: {xml.Length} chars");
    }
}
