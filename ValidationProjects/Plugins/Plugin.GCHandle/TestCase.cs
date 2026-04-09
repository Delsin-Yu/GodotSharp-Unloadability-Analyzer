using System.Runtime.InteropServices;

namespace Plugin.GCHandle;

public class LocalType { public int Value { get; set; } }

public class TestCase
{
    public void Execute()
    {
        // Alloc a plugin-defined type — must root the ALC since GC sees the type reference
        System.Runtime.InteropServices.GCHandle.Alloc(new LocalType { Value = 42 });
    }
}
